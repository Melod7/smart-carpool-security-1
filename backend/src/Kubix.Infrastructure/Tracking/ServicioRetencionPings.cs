using Kubix.Application.Tracking;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kubix.Infrastructure.Tracking;

public sealed class ServicioRetencionPings(ContextoApp db) : IServicioRetencionPings
{
    public static readonly TimeSpan AntiguedadRetencion = TimeSpan.FromDays(7);

    public async Task<int> EjecutarRetencionAsync(CancellationToken ct = default)
    {
        var umbral = DateTimeOffset.UtcNow - AntiguedadRetencion;

        var viajesTerminales = await db.Viajes
            .AsNoTracking()
            .Where(v =>
                (v.Estado == EstadoViaje.Completado || v.Estado == EstadoViaje.Cancelado)
                && (
                    (v.CompletadoEn != null && v.CompletadoEn <= umbral)
                    || (v.CompletadoEn == null && v.ActualizadoEn <= umbral)))
            .Select(v => v.Id)
            .ToListAsync(ct);

        if (viajesTerminales.Count == 0)
        {
            return 0;
        }

        var pings = await db.PingsUbicacion
            .Where(p => viajesTerminales.Contains(p.ViajeId))
            .ToListAsync(ct);

        if (pings.Count == 0)
        {
            return 0;
        }

        var aConservar = pings
            .GroupBy(p => new { p.ViajeId, p.UsuarioId })
            .Select(g => g.OrderByDescending(p => p.RegistradoEn).First().Id)
            .ToHashSet();

        var aBorrar = pings.Where(p => !aConservar.Contains(p.Id)).ToList();
        if (aBorrar.Count == 0)
        {
            return 0;
        }

        db.PingsUbicacion.RemoveRange(aBorrar);
        await db.SaveChangesAsync(ct);
        return aBorrar.Count;
    }
}

/// <summary>
/// Ejecuta la retención de pings periódicamente (cada hora).
/// </summary>
public sealed class TrabajoRetencionPings(
    IServiceScopeFactory scopeFactory,
    ILogger<TrabajoRetencionPings> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var retencion = scope.ServiceProvider.GetRequiredService<IServicioRetencionPings>();
                var borrados = await retencion.EjecutarRetencionAsync(stoppingToken);
                if (borrados > 0)
                {
                    logger.LogInformation("Retención de pings: {Count} filas eliminadas", borrados);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en retención de location_pings");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
