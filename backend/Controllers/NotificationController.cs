using ClassFinder.Api.Services;
using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ClassFinder.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationController(
    IEnrollmentNotificationService enrollmentNotificationService,
    IOptions<NotificationOptions> notificationOptions,
    ILogger<NotificationController> logger
) : ControllerBase
{
    private const string WorkflowKeyHeaderName = "x-classfinder-workflow-key";

    [HttpPost("registration-email")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendRegistrationEmail(
        [FromBody] EnrollmentNotificationMessage message,
        CancellationToken cancellationToken
    )
    {
        var configuredKey = notificationOptions.Value.WorkflowWebhookKey;
        if (
            string.IsNullOrWhiteSpace(configuredKey)
            || !Request.Headers.TryGetValue(WorkflowKeyHeaderName, out var providedKey)
            || !string.Equals(providedKey.ToString(), configuredKey, StringComparison.Ordinal)
        )
        {
            return Unauthorized(new { message = "Workflow key is invalid." });
        }

        try
        {
            await enrollmentNotificationService.SendDirectEmailAsync(message, cancellationToken);
            return Accepted(new { message = "Registration email accepted." });
        }
        catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status429TooManyRequests)
        {
            logger.LogWarning(
                ex,
                "Notification delivery throttled for {RecipientEmail}; returning retryable response.",
                message.RecipientEmail
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Registration email delivery is being retried." }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Notification delivery failed for {RecipientEmail}; returning retryable response.",
                message.RecipientEmail
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Registration email delivery failed and should be retried." }
            );
        }
    }
}
