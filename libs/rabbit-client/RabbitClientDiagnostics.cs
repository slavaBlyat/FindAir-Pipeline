using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FindAir.RabbitClient;

internal static class RabbitClientDiagnostics
{
    public const string ActivitySourceName = "FindAir.RabbitClient";
    public const string MeterName = "FindAir.RabbitClient";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> PublishedMessages =
        Meter.CreateCounter<long>("findair.rabbitmq.messages.published");

    public static readonly Counter<long> PublishFailures =
        Meter.CreateCounter<long>("findair.rabbitmq.messages.publish_failures");

    public static readonly Histogram<double> PublishDurationMs =
        Meter.CreateHistogram<double>("findair.rabbitmq.publish.duration", "ms");

    public static readonly Counter<long> ConsumedMessages =
        Meter.CreateCounter<long>("findair.rabbitmq.messages.consumed");

    public static readonly Counter<long> HandlerFailures =
        Meter.CreateCounter<long>("findair.rabbitmq.messages.handler_failures");

    public static readonly Histogram<double> ProcessingDurationMs =
        Meter.CreateHistogram<double>("findair.rabbitmq.consumer.processing.duration", "ms");

    public static readonly Counter<long> AckedMessages =
        Meter.CreateCounter<long>("findair.rabbitmq.messages.acked");

    public static readonly Counter<long> NackedMessages =
        Meter.CreateCounter<long>("findair.rabbitmq.messages.nacked");

    public static readonly Counter<long> RetriedMessages =
        Meter.CreateCounter<long>("findair.rabbitmq.messages.retried");

    public static readonly Counter<long> ErrorMessages =
        Meter.CreateCounter<long>("findair.rabbitmq.messages.error_routed");

    public static readonly UpDownCounter<long> PublisherChannels =
        Meter.CreateUpDownCounter<long>("findair.rabbitmq.publisher.channels");

    public static readonly Counter<long> ConnectionFailures =
        Meter.CreateCounter<long>("findair.rabbitmq.connection.failures");

    public static readonly Counter<long> ConnectionRecoveries =
        Meter.CreateCounter<long>("findair.rabbitmq.connection.recoveries");

    public static KeyValuePair<string, object?> Tag(string key, object? value) => new(key, value);
}
