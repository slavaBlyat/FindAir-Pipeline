var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "rules-api" }));
app.MapGet("/rules", () => Results.Ok(new[]
{
    new RuleResponse("default-allow", "Default demonstration rule", true)
}));

app.Run();

public sealed record RuleResponse(string Id, string Description, bool Enabled);

