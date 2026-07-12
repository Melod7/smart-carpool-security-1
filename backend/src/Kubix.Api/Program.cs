using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new() { Title = "Kubix UTN API", Version = "v1" });
    });

    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5432;Database=kubix;Username=kubix;Password=kubix";

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    var webOrigin = builder.Configuration["Cors:WebOrigin"] ?? "http://localhost:5173";
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("WebApp", policy =>
            policy.WithOrigins(webOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod());
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors("WebApp");

    if (app.Environment.IsDevelopment() ||
        string.Equals(app.Configuration["Swagger:Enabled"], "true", StringComparison.OrdinalIgnoreCase))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();

    app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        return Results.Ok(new
        {
            status = canConnect ? "healthy" : "degraded",
            service = "kubix-api",
            database = canConnect ? "up" : "down",
            utc = DateTime.UtcNow
        });
    });

    app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok", version = "0.1.0" }));

    var runMigrations = string.Equals(
        app.Configuration["Database:MigrateOnStartup"],
        "true",
        StringComparison.OrdinalIgnoreCase);

    if (runMigrations)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    Log.Information("Kubix API iniciando en {Urls}", string.Join(", ", app.Urls));
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Kubix API terminó de forma inesperada");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
