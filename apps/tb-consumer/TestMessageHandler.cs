using System.Text.Json;
using FindAir.RabbitClient;

internal sealed class TestMessageHandler : IRabbitMessageHandler
{
    private readonly ILogger<TestMessageHandler> _logger;

    public TestMessageHandler(ILogger<TestMessageHandler> logger) => _logger = logger;

    public Task<RabbitMessageProcessingResult> HandleAsync(
        RabbitMessageEnvelope message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var document = JsonDocument.Parse(message.Body);
            if (document.RootElement.TryGetProperty("fail", out var fail) && fail.ValueKind == JsonValueKind.True)
            {
                return Task.FromResult(RabbitMessageProcessingResult.Failure("Test message requested failure."));
            }

            var output = JsonSerializer.SerializeToUtf8Bytes(new
            {
                sourceMessageId = message.MessageId,
                status = "processed",
                processedAtUtc = DateTimeOffset.UtcNow,
                payload = document.RootElement.Clone()
            });
            _logger.LogInformation("Transformed RabbitMQ message {MessageId}", message.MessageId);
            return Task.FromResult(RabbitMessageProcessingResult.Success(output));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Message {MessageId} was not valid JSON", message.MessageId);
            return Task.FromResult(RabbitMessageProcessingResult.Failure("Message body is not valid JSON."));
        }
    }
}

