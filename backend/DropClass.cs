using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.SignalRService;

public class DropClass
{
    private record DropRequest(int StudentId, int ClassId);

    [Function("dropClass")]
    [SignalROutput(HubName = "enrollment", ConnectionStringSetting = "AzureSignalRConnectionString")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "enrollment/drop")] HttpRequestData req,
        out SignalRMessageAction signalRMessage)
    {
        // ✅ v2.0.1: Target is read-only → set via constructor
        signalRMessage = new SignalRMessageAction("ScheduleUpdated")
        {
            GroupName = "placeholder",
            Arguments = new object[] { }
        };

        // ✅ Read body synchronously (so method isn't async)
        string body;
        using (var reader = new StreamReader(req.Body))
            body = reader.ReadToEnd();

        DropRequest? data;
        try
        {
            data = JsonSerializer.Deserialize<DropRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            var badJson = req.CreateResponse(HttpStatusCode.BadRequest);
            badJson.WriteString("Invalid JSON body.");
            return badJson;
        }

        if (data == null || data.StudentId <= 0 || data.ClassId <= 0)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            bad.WriteString("Body must be: { \"studentId\": 1, \"classId\": 101 }");
            return bad;
        }

        // Sprint 1: broadcast that a drop happened (swap in real DB logic later)
        signalRMessage.GroupName = $"student-{data.StudentId}";
        signalRMessage.Arguments = new object[]
        {
            new
            {
                action = "DROP",
                studentId = data.StudentId,
                classId = data.ClassId,
                at = DateTimeOffset.UtcNow
            }
        };

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json");
        ok.WriteString(JsonSerializer.Serialize(new { message = "Drop broadcast sent." }));
        return ok;
    }
}