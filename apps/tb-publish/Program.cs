using System.Text.Json;
using FindAir.RabbitClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRabbitPublisher(builder.Configuration);
builder.Services.AddHostedService<PublishOnceService>();
await builder.Build().RunAsync();

internal sealed class PublishOnceService : BackgroundService
{
    private readonly IRabbitPublisher _publisher;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<PublishOnceService> _logger;

    public PublishOnceService(
        IRabbitPublisher publisher,
        IHostApplicationLifetime lifetime,
        ILogger<PublishOnceService> logger)
    {
        _publisher = publisher;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                id = Guid.NewGuid(),
                text = "Test message from tb-publish",
                fail = false,
                publishedAtUtc = DateTimeOffset.UtcNow
            });
            var message = RabbitMessageEnvelope.FromUtf8(payload);
            await _publisher.PublishToInputAsync(message, stoppingToken);
            _logger.LogInformation("Published test message {MessageId}", message.MessageId);
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
