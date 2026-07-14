using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kubix.Application.Viajes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kubix.Infrastructure.Viajes;

public sealed class ServicioDirectionsGoogle(
    HttpClient http,
    IOptions<OpcionesGoogleMaps> opciones,
    ILogger<ServicioDirectionsGoogle> logger) : IServicioDirections
{
    private static readonly JsonSerializerOptions JsonOpciones = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ResultadoDirections?> ObtenerRutaAsync(
        double origenLat,
        double origenLng,
        double destinoLat,
        double destinoLng,
        IReadOnlyList<(double Lat, double Lng)>? vias = null,
        CancellationToken ct = default)
    {
        var apiKey = opciones.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogDebug("Google Maps API key ausente; Directions tratado como fallo.");
            return null;
        }

        var origen = FormatearCoord(origenLat, origenLng);
        var destino = FormatearCoord(destinoLat, destinoLng);
        var url =
            $"https://maps.googleapis.com/maps/api/directions/json?origin={origen}&destination={destino}&key={apiKey}";

        if (vias is { Count: > 0 })
        {
            var viasParam = string.Join(
                '|',
                vias.Select(v => FormatearCoord(v.Lat, v.Lng)));
            url += $"&waypoints={Uri.EscapeDataString(viasParam)}";
        }

        try
        {
            using var respuesta = await http.GetAsync(url, ct);
            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Google Directions HTTP {Status}",
                    (int)respuesta.StatusCode);
                return null;
            }

            await using var stream = await respuesta.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<RespuestaDirectionsGoogle>(
                stream,
                JsonOpciones,
                ct);

            if (payload is null
                || !string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase)
                || payload.Routes is null
                || payload.Routes.Count == 0)
            {
                logger.LogWarning(
                    "Google Directions status={Status}",
                    payload?.Status ?? "null");
                return null;
            }

            var ruta = payload.Routes[0];
            var polilinea = ruta.OverviewPolyline?.Points;
            if (string.IsNullOrWhiteSpace(polilinea))
            {
                return null;
            }

            var metros = ruta.Legs?.Sum(l => l.Distance?.Value ?? 0) ?? 0;
            if (metros <= 0)
            {
                return null;
            }

            return new ResultadoDirections
            {
                Polilinea = polilinea,
                DistanciaKm = Math.Round(metros / 1000m, 3, MidpointRounding.AwayFromZero)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al llamar Google Directions");
            return null;
        }
    }

    private static string FormatearCoord(double lat, double lng) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}");

    private sealed class RespuestaDirectionsGoogle
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("routes")]
        public List<RutaGoogle>? Routes { get; set; }
    }

    private sealed class RutaGoogle
    {
        [JsonPropertyName("overview_polyline")]
        public PolilineaGoogle? OverviewPolyline { get; set; }

        [JsonPropertyName("legs")]
        public List<TramoGoogle>? Legs { get; set; }
    }

    private sealed class PolilineaGoogle
    {
        [JsonPropertyName("points")]
        public string? Points { get; set; }
    }

    private sealed class TramoGoogle
    {
        [JsonPropertyName("distance")]
        public DistanciaGoogle? Distance { get; set; }
    }

    private sealed class DistanciaGoogle
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }
    }
}
