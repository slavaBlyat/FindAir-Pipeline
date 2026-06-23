using FindAir.RabbitClient;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRabbitConsumer(builder.Configuration);
builder.Services.AddSingleton<IRabbitMessageHandler, TestMessageHandler>();
builder.Services.AddHostedService<ConsumerWorker>();
await builder.Build().RunAsync();
