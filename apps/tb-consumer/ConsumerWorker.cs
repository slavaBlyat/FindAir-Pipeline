using FindAir.RabbitClient;
using Microsoft.Extensions.Options;

internal sealed class ConsumerWorker : BackgroundService
{
    private readonly IRabbitConsumer _consumer;
    private readonly IRabbitMessageHandler _handler;
    private readonly RabbitClientOptions _options;
    private readonly ILogger<ConsumerWorker> _logger;

    public ConsumerWorker(
        IRabbitConsumer consumer,
        IRabbitMessageHandler handler,
        IOptions<RabbitClientOptions> options,
        ILogger<ConsumerWorker> logger)
    {
        _consumer = consumer;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting RabbitMQ consumer worker for queue {InputQueue}", _options.InputQueue);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _consumer.ConsumeAsync(_handler, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ consumer stopped unexpectedly; restarting after delay");
                await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
            }
        }
    }
}
