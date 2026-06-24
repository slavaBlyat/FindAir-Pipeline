# FindAir

FindAir is an Nx-managed .NET 10 monorepo for a small RabbitMQ message flow. It
contains one HTTP API, RabbitMQ publisher components, one background consumer,
and a shared RabbitMQ client library.

The repo is intentionally simple: Nx orchestrates commands, while .NET owns the
actual build, restore, run, and test behavior.

## Repository map

| Path | Type | Purpose |
| --- | --- | --- |
| `apps/gateway` | RabbitMQ publisher endpoint | Health endpoint and test-message publishing endpoint |
| `apps/rules-api` | HTTP API | Demo rules endpoint and health endpoint; does not register RabbitMQ |
| `apps/tb-publish` | Worker/console app | Publishes one test message and exits |
| `apps/tb-consumer` | Worker service | Consumes input messages and routes success/error outcomes |
| `libs/rabbit-client` | Class library | Shared RabbitMQ publisher, consumer, batch consumer, and connection manager |

## Runtime flow

1. `gateway` receives `POST /publish-test`.
2. `gateway` publishes a JSON message to `tb.input` through `rabbit-client`.
3. `tb-consumer` consumes from `tb.input`.
4. `TestMessageHandler` transforms valid messages.
5. Successful output is published to `tb.output`.
6. Failed messages are routed to `tb.error`, unless retry requeue is enabled.
7. `rules-api` is the only pure HTTP API and is independent from RabbitMQ.

## Version management

| Concern | File | Current value |
| --- | --- | --- |
| .NET SDK | `global.json` | `10.0.300` |
| Target framework | `Directory.Build.props` | `net10.0` |
| Nx | `package.json` | `23.0.0` |
| RabbitMQ client package | `libs/rabbit-client/RabbitClient.csproj` | `RabbitMQ.Client` `7.2.1` |

Package versions are currently declared inside each `.csproj`. If this repo
grows, prefer moving NuGet versions into `Directory.Packages.props`.

## Prerequisites

- .NET SDK `10.0.300`
- Node.js supported by Nx: `20.19+`, `22.12+`, or `24+`
- npm
- A reachable RabbitMQ broker

Useful checks:

```bash
dotnet --version
dotnet --list-sdks
node --version
npm --version
npx nx show projects
```

On Windows, install .NET with:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --version 10.0.300
```

## Install and validate

```bash
npm install
dotnet restore FindAir.sln
dotnet build FindAir.sln --no-restore
dotnet test FindAir.sln --no-build
npx nx run-many -t build
```

Nx build targets use `--no-restore`, so run `dotnet restore` after a fresh clone
or after package changes.

## Run locally

RabbitMQ is not provisioned by this repository. Start or connect to a broker
outside the repo, then point the apps at it with `RabbitMq` configuration.

Default local RabbitMQ endpoints:

| Endpoint | Value |
| --- | --- |
| AMQP | `localhost:5672` |
| Management UI | `http://localhost:15672` |
| Local credentials | `guest` / `guest` |

Run services with Nx:

```bash
npx nx serve tb-consumer
npx nx serve gateway
npx nx serve rules-api
npx nx serve tb-publish
```

Equivalent .NET commands:

```bash
dotnet run --project apps/tb-consumer/TbConsumer.csproj
dotnet run --project apps/gateway/Gateway.csproj
dotnet run --project apps/rules-api/RulesApi.csproj
dotnet run --project apps/tb-publish/TbPublish.csproj
```

Exercise the flow:

```bash
curl http://localhost:5000/health
curl -X POST http://localhost:5000/publish-test \
  -H "Content-Type: application/json" \
  -d '{"text":"hello","fail":false}'
curl http://localhost:5001/rules
```

Set `fail` to `true` to route the message to the error queue.

## Nx layout

Every app/lib has a `project.json` file. Each target uses `nx:run-commands` and
delegates to the .NET CLI:

| Nx target | What it does |
| --- | --- |
| `build` | Runs `dotnet build <project>.csproj --no-restore` |
| `serve` | Runs `dotnet run --project <project>.csproj` |

Useful commands:

```bash
npx nx show projects
npx nx graph
npx nx build rabbit-client
npx nx run-many -t build
```

## RabbitMQ configuration

RabbitMQ settings live under `RabbitMq` in each participating app's
`appsettings.json`. Publisher apps use only publisher settings; the consumer app
adds consumer-only queue and concurrency settings.

Publisher-only apps (`gateway`, `tb-publish`) register `AddRabbitPublisher(...)`:

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "InputQueue": "tb.input",
    "PublisherChannelPoolSize": 4,
    "ReconnectDelaySeconds": 5,
    "MaxRetryCount": 3
  }
}
```

The consumer app (`tb-consumer`) registers `AddRabbitConsumer(...)`:

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "InputQueue": "tb.input",
    "OutputQueue": "tb.output",
    "ErrorQueue": "tb.error",
    "PrefetchCount": 10,
    "ConsumerCount": 1,
    "BatchSize": 25,
    "PublisherChannelPoolSize": 4,
    "ReconnectDelaySeconds": 5,
    "MaxRetryCount": 3,
    "RequeueOnFailure": false
  }
}
```

Environment overrides use standard .NET double-underscore keys:

```bash
export RabbitMq__Host=rabbitmq
export RabbitMq__Password=change-me
export RabbitMq__RequeueOnFailure=true
```

Do not commit production credentials. Use environment variables, user secrets,
or a secrets provider.

## Connections and channels by app

The shared library registers one singleton `RabbitConnectionManager` per app
process. That manager lazily creates one RabbitMQ TCP connection and reuses it
inside the process.

| App | Connection behavior | Channel behavior |
| --- | --- | --- |
| `gateway` | Opens one process-level connection on first publish | Leases a publish channel from the bounded channel pool, declares the target queue, publishes with confirmations, then returns the channel to the pool |
| `tb-publish` | Opens one process-level connection during the one-shot publish | Leases one publish channel, publishes with confirmations, returns the channel, then exits |
| `tb-consumer` | Opens one process-level connection when the worker starts | Creates `ConsumerCount` long-lived consumer channels, declares input/output/error queues, applies prefetch, consumes with manual ack/nack |
| `rules-api` | Does not register or use RabbitMQ | No RabbitMQ channels |

Important details:

- A RabbitMQ connection is expensive and should normally be shared per process.
- A RabbitMQ channel is lightweight and should not be shared across unrelated
  concurrent operations.
- Publishing uses a bounded channel pool because creating a channel per message
  is wasteful at high throughput. Each publish leases a channel exclusively.
- Consuming uses a long-lived channel because delivery tags, acknowledgements,
  prefetch, and `BasicConsume` are channel-scoped.
- `tb-consumer` acknowledges an input message only after output/error routing
  succeeds. If completion fails, it nacks the message with requeue enabled.
- The client enables RabbitMQ automatic connection and topology recovery, but
  the app worker still restarts consumption if `ConsumeAsync` exits.

## Observability

The RabbitMQ client emits traces and metrics without taking a hard dependency on
an exporter. Hosting apps can wire OpenTelemetry to the names below:

| Signal | Name |
| --- | --- |
| Activity source | `FindAir.RabbitClient` |
| Meter | `FindAir.RabbitClient` |

Important metrics:

| Metric | Meaning |
| --- | --- |
| `findair.rabbitmq.messages.published` | Confirmed publish count |
| `findair.rabbitmq.messages.publish_failures` | Failed publish attempts |
| `findair.rabbitmq.publish.duration` | Publish duration in milliseconds |
| `findair.rabbitmq.messages.consumed` | Delivered messages received by consumers |
| `findair.rabbitmq.consumer.processing.duration` | Handler processing time in milliseconds |
| `findair.rabbitmq.messages.acked` | Acked messages by outcome |
| `findair.rabbitmq.messages.nacked` | Nacked messages |
| `findair.rabbitmq.messages.retried` | Messages republished for retry |
| `findair.rabbitmq.messages.error_routed` | Messages routed to the error queue |
| `findair.rabbitmq.publisher.channels` | Active publisher channels in the pool |
| `findair.rabbitmq.connection.failures` | Failed connection attempts |
| `findair.rabbitmq.connection.recoveries` | Successful connection creations/recoveries |

## RabbitMQ delivery behavior

- Publisher confirmations are enabled for publish operations.
- Input, output, and error queues are declared as durable queues.
- Consumers use manual acknowledgements.
- `PrefetchCount` limits unacknowledged messages per consumer channel.
- `RequeueOnFailure=false` sends failed messages to `ErrorQueue`.
- `RequeueOnFailure=true` republishes failed messages to `InputQueue` with an
  incremented `x-findair-retry-count` header until `MaxRetryCount` is reached.
- Delivery is at-least-once, not exactly-once. Handlers should be idempotent.

## Production notes

The current repo is good for a small demo, but it is not production-complete yet.
Before production use, add CI, tests, deployment manifests, centralized package
management, structured health checks, metrics/tracing, dead-letter topology,
and real secret management.

See `libs/rabbit-client/README.md` for more RabbitMQ client API details.
