namespace Kubix.Application.Viajes;

public interface IServicioDirections
{
    /// <summary>
    /// Obtiene polilínea encoded y distancia entre origen y destino.
    /// <paramref name="vias"/> son waypoints intermedios (WP1..WPn-1); el destino es el campus.
    /// Devuelve null si Directions no está disponible / falla.
    /// </summary>
    Task<ResultadoDirections?> ObtenerRutaAsync(
        double origenLat,
        double origenLng,
        double destinoLat,
        double destinoLng,
        IReadOnlyList<(double Lat, double Lng)>? vias = null,
        CancellationToken ct = default);
}

public sealed class ResultadoDirections
{
    public string Polilinea { get; init; } = string.Empty;
    public decimal DistanciaKm { get; init; }
}
