namespace Kubix.Application.Tracking;

public interface IServicioRetencionPings
{
    /// <summary>
    /// Borra pings de viajes terminales con antigüedad &gt; 7 días,
    /// conservando el último ping por usuario por viaje.
    /// </summary>
    /// <returns>Cantidad de pings eliminados.</returns>
    Task<int> EjecutarRetencionAsync(CancellationToken ct = default);
}
