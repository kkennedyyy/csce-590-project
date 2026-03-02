using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.SignalRService;
using System.Net;

public class JoinStudentGroup
{
    [Function("joinStudentGroup")]
    [SignalROutput(HubName = "enrollment", ConnectionStringSetting = "AzureSignalRConnectionString")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "students/{studentId:int}/join")] HttpRequestData req,
        int studentId,
        out SignalRGroupAction signalRAction)
    {
        // ✅ Always initialize using the required constructor (prevents CS7036 on any path)
        signalRAction = new SignalRGroupAction(SignalRGroupActionType.Add)
        {
            ConnectionId = "placeholder",
            GroupName = "placeholder"
        };

        if (!req.Headers.TryGetValues("x-signalr-connectionid", out var values))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            bad.WriteString("Missing header: x-signalr-connectionid");
            return bad;
        }

        var connectionId = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            bad.WriteString("Empty header: x-signalr-connectionid");
            return bad;
        }

        // ✅ Update the initialized action with real values
        signalRAction.ConnectionId = connectionId;
        signalRAction.GroupName = $"student-{studentId}";

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.WriteString("Joined student group.");
        return ok;
    }
}