using System.Net.Mail;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Communication.Email;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace ClassFinder.Api.Services;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; } = true;
    public bool DirectEmailEnabled { get; set; }
    public string FromEmail { get; set; } = "noreply@classfinder.local";
    public string FromDisplayName { get; set; } = "ClassFinder";
    public string? PickupDirectory { get; set; } = "App_Data/maildrop";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 25;
    public string? AzureCommunicationConnectionString { get; set; }
    public string? AzureCommunicationSenderAddress { get; set; }
    public string? OutlookConnectionResourceId { get; set; }
    public string? ServiceBusConnectionString { get; set; }
    public string? ServiceBusEntityName { get; set; } = "classfinder-registration-events";
    public string? WorkflowWebhookKey { get; set; }
}

public class EnrollmentNotificationService(
    IOptions<NotificationOptions> options,
    IHostEnvironment hostEnvironment,
    IHttpClientFactory httpClientFactory,
    ILogger<EnrollmentNotificationService> logger
) : IEnrollmentNotificationService
{
    private static readonly TokenRequestContext ArmTokenRequestContext = new(
        ["https://management.azure.com/.default"]
    );
    private readonly DefaultAzureCredential armCredential = new();

    public async Task SendEnrollmentReceiptAsync(
        EnrollmentNotificationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation(
                "Registration notifications are disabled; skipped {Action} confirmation for {RecipientEmail}.",
                message.Action,
                message.RecipientEmail
            );
            return;
        }

        var delivered = false;

        if (
            !string.IsNullOrWhiteSpace(options.Value.ServiceBusConnectionString)
            && !string.IsNullOrWhiteSpace(options.Value.ServiceBusEntityName)
        )
        {
            try
            {
                await PublishToServiceBusAsync(message, cancellationToken);
                delivered = true;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to publish registration event for {RecipientEmail} and class {ClassId}.",
                    message.RecipientEmail,
                    message.ClassId
                );
            }
        }

        if (!options.Value.DirectEmailEnabled)
        {
            if (!delivered)
            {
                logger.LogWarning(
                    "No notification transport succeeded for {RecipientEmail}. Configure Service Bus or enable direct email.",
                    message.RecipientEmail
                );
            }

            return;
        }

        await SendDirectEmailAsync(message, cancellationToken);
    }

    public async Task SendDirectEmailAsync(
        EnrollmentNotificationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation(
                "Registration notifications are disabled; skipped direct email for {RecipientEmail}.",
                message.RecipientEmail
            );
            return;
        }

        try
        {
            if (CanSendWithOutlookConnection())
            {
                await SendWithOutlookConnectionAsync(message, cancellationToken);
            }
            else if (CanSendWithAzureCommunication())
            {
                await SendWithAzureCommunicationAsync(message, cancellationToken);
            }
            else
            {
                using var mailMessage = BuildMailMessage(message);
                using var smtpClient = BuildClient();
                smtpClient.Send(mailMessage);
            }

            logger.LogInformation(
                "Registration email queued for {RecipientEmail} with action {Action} for {ClassId}.",
                message.RecipientEmail,
                message.Action,
                message.ClassId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send registration email for {RecipientEmail} and class {ClassId}.",
                message.RecipientEmail,
                message.ClassId
            );
            throw;
        }
    }

    private async Task PublishToServiceBusAsync(
        EnrollmentNotificationMessage message,
        CancellationToken cancellationToken
    )
    {
        await using var client = new ServiceBusClient(options.Value.ServiceBusConnectionString);
        await using var sender = client.CreateSender(options.Value.ServiceBusEntityName);

        var payload = JsonSerializer.Serialize(message);
        var serviceBusMessage = new ServiceBusMessage(payload)
        {
            ContentType = "application/json",
            Subject = $"registration.{message.Action}"
        };
        serviceBusMessage.ApplicationProperties["eventType"] = $"registration.{message.Action}";
        serviceBusMessage.ApplicationProperties["studentId"] = message.StudentId;
        serviceBusMessage.ApplicationProperties["classId"] = message.ClassId;

        await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

        logger.LogInformation(
            "Registration event published to Service Bus for {RecipientEmail} with action {Action} for {ClassId}.",
            message.RecipientEmail,
            message.Action,
            message.ClassId
        );
    }

    private bool CanSendWithAzureCommunication() =>
        !string.IsNullOrWhiteSpace(options.Value.AzureCommunicationConnectionString)
        && !string.IsNullOrWhiteSpace(options.Value.AzureCommunicationSenderAddress);

    private bool CanSendWithOutlookConnection() =>
        !string.IsNullOrWhiteSpace(options.Value.OutlookConnectionResourceId);

    private async Task SendWithOutlookConnectionAsync(
        EnrollmentNotificationMessage message,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await armCredential.GetTokenAsync(ArmTokenRequestContext, cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://management.azure.com{options.Value.OutlookConnectionResourceId}/dynamicInvoke?api-version=2018-07-01-preview"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

        var payload = new Dictionary<string, object?>
        {
            ["request"] = new Dictionary<string, object?>
            {
                ["method"] = "post",
                ["path"] = "/v2/Mail",
                ["body"] = new Dictionary<string, object?>
                {
                    ["To"] = message.RecipientEmail,
                    ["Subject"] = BuildSubject(message),
                    ["Body"] = BuildHtmlBody(message),
                    ["Importance"] = "Normal"
                }
            }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        using var client = httpClientFactory.CreateClient(nameof(EnrollmentNotificationService));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "Outlook connection accepted email delivery for {RecipientEmail}.",
                message.RecipientEmail
            );
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Outlook connection send failed with status {(int)response.StatusCode}: {responseBody}",
            null,
            response.StatusCode
        );
    }

    private async Task SendWithAzureCommunicationAsync(
        EnrollmentNotificationMessage message,
        CancellationToken cancellationToken
    )
    {
        var emailClient = BuildEmailClient();
        var emailContent = new EmailContent(BuildSubject(message))
        {
            PlainText = BuildPlainTextBody(message)
        };
        var recipients = new EmailRecipients(
            new List<EmailAddress> { new(message.RecipientEmail, message.RecipientName) }
        );
        var emailMessage = new EmailMessage(
            options.Value.AzureCommunicationSenderAddress,
            recipients,
            emailContent
        );

        await emailClient.SendAsync(
            WaitUntil.Started,
            emailMessage,
            cancellationToken
        );

        logger.LogInformation(
            "Azure Communication Services accepted email delivery for {RecipientEmail}.",
            message.RecipientEmail
        );
    }

    private MailMessage BuildMailMessage(EnrollmentNotificationMessage message)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress(options.Value.FromEmail, options.Value.FromDisplayName),
            Subject = BuildSubject(message),
            Body = BuildPlainTextBody(message),
            IsBodyHtml = false
        };

        mailMessage.To.Add(new MailAddress(message.RecipientEmail, message.RecipientName));
        return mailMessage;
    }

    private string BuildPlainTextBody(EnrollmentNotificationMessage message)
    {
        var subjectAction = char.ToUpperInvariant(message.Action[0]) + message.Action[1..].ToLowerInvariant();
        return $"""
            Hello {message.RecipientName},

            Your class registration request has completed successfully.

            Action: {subjectAction}
            Student ID: {message.StudentId}
            Class: {message.ClassId} - {message.ClassTitle}
            Department: {message.Department}
            Instructor: {message.Instructor}
            Schedule: {message.ScheduleSummary}
            Location: {message.Location}
            Credits: {message.Credits}
            Available Seats: {message.AvailableSeats}
            Completed At (UTC): {message.OccurredAtUtc:yyyy-MM-dd HH:mm:ss}

            If this change was unexpected, contact the registrar immediately.

            - ClassFinder
            """;
    }

    private string BuildHtmlBody(EnrollmentNotificationMessage message)
    {
        var subjectAction = char.ToUpperInvariant(message.Action[0]) + message.Action[1..].ToLowerInvariant();
        return $"""
            <p>Hello {message.RecipientName},</p>
            <p>Your class registration request has completed successfully.</p>
            <p>
            <strong>Action:</strong> {subjectAction}<br/>
            <strong>Student ID:</strong> {message.StudentId}<br/>
            <strong>Class:</strong> {message.ClassId} - {message.ClassTitle}<br/>
            <strong>Department:</strong> {message.Department}<br/>
            <strong>Instructor:</strong> {message.Instructor}<br/>
            <strong>Schedule:</strong> {message.ScheduleSummary}<br/>
            <strong>Location:</strong> {message.Location}<br/>
            <strong>Credits:</strong> {message.Credits}<br/>
            <strong>Available Seats:</strong> {message.AvailableSeats}<br/>
            <strong>Completed At (UTC):</strong> {message.OccurredAtUtc:yyyy-MM-dd HH:mm:ss}
            </p>
            <p>If this change was unexpected, contact the registrar immediately.</p>
            <p>ClassFinder</p>
            """;
    }

    private string BuildSubject(EnrollmentNotificationMessage message)
    {
        var subjectAction = char.ToUpperInvariant(message.Action[0]) + message.Action[1..].ToLowerInvariant();
        return $"ClassFinder {subjectAction} Confirmation - {message.ClassId}";
    }

    private SmtpClient BuildClient()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.SmtpHost))
        {
            return new SmtpClient(options.Value.SmtpHost, options.Value.SmtpPort);
        }

        var pickupDirectory = ResolvePath(options.Value.PickupDirectory, Path.Combine("App_Data", "maildrop"));
        Directory.CreateDirectory(pickupDirectory);

        return new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickupDirectory
        };
    }

    private EmailClient BuildEmailClient()
    {
        var clientOptions = new EmailClientOptions();
        clientOptions.Retry.Mode = RetryMode.Exponential;
        clientOptions.Retry.Delay = TimeSpan.FromSeconds(2);
        clientOptions.Retry.MaxDelay = TimeSpan.FromSeconds(20);
        clientOptions.Retry.MaxRetries = 6;
        clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(100);

        return new EmailClient(options.Value.AzureCommunicationConnectionString, clientOptions);
    }

    private string ResolvePath(string? configuredPath, string fallbackRelativePath)
    {
        var value = string.IsNullOrWhiteSpace(configuredPath) ? fallbackRelativePath : configuredPath.Trim();
        return Path.IsPathRooted(value) ? value : Path.Combine(hostEnvironment.ContentRootPath, value);
    }
}
