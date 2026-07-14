namespace Kubix.Application.Viajes;

/// <summary>
/// Decodifica polilíneas encoded de Google Maps (overview_polyline).
/// </summary>
public static class DecodificadorPolilineaGoogle
{
    public static IReadOnlyList<(double Lat, double Lng)> Decodificar(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return Array.Empty<(double, double)>();
        }

        var puntos = new List<(double Lat, double Lng)>();
        var index = 0;
        var lat = 0;
        var lng = 0;

        while (index < encoded.Length)
        {
            lat += DecodificarDelta(encoded, ref index);
            lng += DecodificarDelta(encoded, ref index);
            puntos.Add((lat / 1e5, lng / 1e5));
        }

        return puntos;
    }

    private static int DecodificarDelta(string encoded, ref int index)
    {
        var result = 0;
        var shift = 0;
        int b;

        do
        {
            if (index >= encoded.Length)
            {
                break;
            }

            b = encoded[index++] - 63;
            result |= (b & 0x1f) << shift;
            shift += 5;
        } while (b >= 0x20);

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
