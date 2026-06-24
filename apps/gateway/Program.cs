using System.Text.Json;
using FindAir.RabbitClient;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRabbitPublisher(builder.Configuration);

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway" }));
app.MapPost("/publish-test", async (
    PublishTestRequest? request,
    IRabbitPublisher publisher,
    CancellationToken cancellationToken) =>
{
    var payload = new
    {
        id = Guid.NewGuid(),
        text = request?.Text ?? "FindAir test message",
        fail = request?.Fail ?? false,
        publishedAtUtc = DateTimeOffset.UtcNow
    };
    var message = RabbitMessageEnvelope.FromUtf8(JsonSerializer.Serialize(payload));
    await publisher.PublishToInputAsync(message, cancellationToken);
    return Results.Accepted(value: new { message.MessageId });
});
app.Run();

public sealed record PublishTestRequest(string? Text, bool Fail = false);
