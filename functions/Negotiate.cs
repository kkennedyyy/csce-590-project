using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.SignalRService;

public class Negotiate
{
    [Function("negotiate")]
    public SignalRConnectionInfo Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "negotiate")] HttpRequestData req,
        [SignalRConnectionInfoInput(HubName = "enrollment", ConnectionStringSetting = "AzureSignalRConnectionString")]
        SignalRConnectionInfo connectionInfo)
        => connectionInfo;
}