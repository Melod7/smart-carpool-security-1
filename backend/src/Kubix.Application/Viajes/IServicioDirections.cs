namespace Kubix.Application.Viajes;

public interface IServicioDirections
{
    /// <summary>
    /// Obtiene polilínea encoded y distancia entre origen y destino.
    /// Devuelve null o lanza si Directions no está disponible / falla.
    /// </summary>
    Task<ResultadoDirections?> ObtenerRutaAsync(
        double origenLat,
        double origenLng,
        double destinoLat,
        double destinoLng,
        CancellationToken ct = default);
}

public sealed class ResultadoDirections
{
    public string Polilinea { get; init; } = string.Empty;
    public decimal DistanciaKm { get; init; }
}
