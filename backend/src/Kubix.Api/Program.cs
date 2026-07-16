using System.Text;
using Kubix.Api.Middleware;
using Kubix.Infrastructure.Admin;
using Kubix.Infrastructure.Auth;
using Kubix.Infrastructure.Calificaciones;
using Kubix.Infrastructure.EcoTokens;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Sos;
using Kubix.Infrastructure.SuperAdmin;
using Kubix.Infrastructure.Tenancy;
using Kubix.Infrastructure.Tracking;
using Kubix.Infrastructure.Usuarios;
using Kubix.Infrastructure.Viajes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Bearer. Ejemplo: Bearer {token}"
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });

    var cadenaConexion = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5432;Database=kubix;Username=kubix;Password=kubix";

    builder.Services.AddDbContext<ContextoApp>(options =>
        options.UseNpgsql(cadenaConexion));
    builder.Services.AddScoped<SembradorBaseDatos>();
    builder.Services.AgregarServiciosAuth(builder.Configuration);
    builder.Services.AgregarTenancy();
    builder.Services.AgregarSuperAdmin();
    builder.Services.AgregarRegistroUsuarios(builder.Configuration);
    builder.Services.AgregarViajes(builder.Configuration);
    builder.Services.AgregarCalificaciones();
    builder.Services.AgregarEcoTokens();
    builder.Services.AgregarSos();
    builder.Services.AgregarTracking();
    builder.Services.AgregarAdminOps();

    var jwt = builder.Configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
        });

    // Puertos fijos locales: Vite :5173, Flutter web :5055.
    // Cors:WebOrigin se mantiene por compatibilidad con docker-compose / .env.
    var origenes = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (origenes is null || origenes.Length == 0)
    {
        var origenWeb = builder.Configuration["Cors:WebOrigin"] ?? "http://localhost:5173";
        origenes = [origenWeb];
    }

    static bool EsOrigenLocalDeDesarrollo(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        // Solo hosts loopback en Development (puertos fijos preferidos: 5173 / 5055).
        return uri.Host is "localhost" or "127.0.0.1" or "[::1]" or "::1";
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("WebApp", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(EsOrigenLocalDeDesarrollo)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
            else
            {
                policy.WithOrigins(origenes)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Chrome Private Network Access: preflight con
    // Access-Control-Request-Private-Network cuando la página es localhost
    // y la API se llama por 127.0.0.1 (u otro loopback distinto).
    if (app.Environment.IsDevelopment())
    {
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsOptions(context.Request.Method) &&
                string.Equals(
                    context.Request.Headers["Access-Control-Request-Private-Network"],
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            }

            await next();
        });
    }

    app.UseCors("WebApp");

    // CloudFront reserva /api/* para el ALB. Internamente los controladores
    // conservan sus rutas históricas (/auth, /admin, /me, etc.).
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api", out var remaining)
            && context.Request.Path != "/api/v1/health")
        {
            context.Request.Path = remaining.HasValue ? remaining : "/";
        }

        await next();
    });
    app.UseRouting();

    if (app.Environment.IsDevelopment() ||
        string.Equals(app.Configuration["Swagger:Enabled"], "true", StringComparison.OrdinalIgnoreCase))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseMiddleware<MiddlewareInquilino>();
    app.UseAuthorization();
    app.UseMiddleware<MiddlewareEstadoSesion>();

    app.MapControllers();

    app.MapGet("/health", async (ContextoApp db, CancellationToken ct) =>
    {
        var puedeConectar = await db.Database.CanConnectAsync(ct);
        return Results.Ok(new
        {
            status = puedeConectar ? "healthy" : "degraded",
            service = "kubix-api",
            database = puedeConectar ? "up" : "down",
            utc = DateTime.UtcNow
        });
    });

    app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok", version = "0.1.0" }));

    var ejecutarMigraciones = string.Equals(
        app.Configuration["Database:MigrateOnStartup"],
        "true",
        StringComparison.OrdinalIgnoreCase);
    var ejecutarSeed = string.Equals(
        app.Configuration["Database:SeedOnStartup"],
        "true",
        StringComparison.OrdinalIgnoreCase);

    if (ejecutarMigraciones || ejecutarSeed)
    {
        using var ambito = app.Services.CreateScope();
        if (ejecutarMigraciones)
        {
            var db = ambito.ServiceProvider.GetRequiredService<ContextoApp>();
            await db.Database.MigrateAsync();
        }

        if (ejecutarSeed)
        {
            var sembrador = ambito.ServiceProvider.GetRequiredService<SembradorBaseDatos>();
            await sembrador.SembrarAsync();
        }
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
