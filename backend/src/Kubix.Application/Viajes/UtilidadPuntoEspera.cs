namespace Kubix.Application.Viajes;

public sealed class ResultadoPuntoEspera
{
    public double Lat { get; init; }
    public double Lng { get; init; }
    public double DistanciaM { get; init; }
    public int SegmentIndex { get; init; }
    public bool TooFar { get; init; }
}

/// <summary>
/// Proyecta la ubicación del pasajero sobre la geometría de la ruta (polilínea o waypoints+campus).
/// </summary>
public static class UtilidadPuntoEspera
{
    public const double UmbralTooFarMetros = 800.0;

    public static ResultadoPuntoEspera Calcular(
        double pasajeroLat,
        double pasajeroLng,
        string? polilinea,
        IReadOnlyList<(double Lat, double Lng)> waypoints,
        double campusLat,
        double campusLng,
        double umbralTooFarMetros = UmbralTooFarMetros)
    {
        var ruta = ConstruirPuntosRuta(polilinea, waypoints, campusLat, campusLng);
        if (ruta.Count == 0)
        {
            return new ResultadoPuntoEspera
            {
                Lat = campusLat,
                Lng = campusLng,
                DistanciaM = UtilidadHaversine.DistanciaMetros(pasajeroLat, pasajeroLng, campusLat, campusLng),
                SegmentIndex = 0,
                TooFar = true
            };
        }

        if (ruta.Count == 1)
        {
            var d = UtilidadHaversine.DistanciaMetros(pasajeroLat, pasajeroLng, ruta[0].Lat, ruta[0].Lng);
            return new ResultadoPuntoEspera
            {
                Lat = ruta[0].Lat,
                Lng = ruta[0].Lng,
                DistanciaM = d,
                SegmentIndex = 0,
                TooFar = d > umbralTooFarMetros
            };
        }

        var mejorDist = double.MaxValue;
        var mejorLat = ruta[0].Lat;
        var mejorLng = ruta[0].Lng;
        var mejorSeg = 0;

        // Proyección equirectangular local anclada al pasajero.
        var cosLat = Math.Cos(ARadianes(pasajeroLat));
        var px = 0.0;
        var py = 0.0;

        for (var i = 0; i < ruta.Count - 1; i++)
        {
            var a = ruta[i];
            var b = ruta[i + 1];
            var ax = (a.Lng - pasajeroLng) * cosLat;
            var ay = a.Lat - pasajeroLat;
            var bx = (b.Lng - pasajeroLng) * cosLat;
            var by = b.Lat - pasajeroLat;

            var dx = bx - ax;
            var dy = by - ay;
            var len2 = dx * dx + dy * dy;
            double t;
            if (len2 <= 0)
            {
                t = 0;
            }
            else
            {
                t = ((px - ax) * dx + (py - ay) * dy) / len2;
                t = Math.Clamp(t, 0, 1);
            }

            var qx = ax + t * dx;
            var qy = ay + t * dy;
            var projLat = pasajeroLat + qy;
            var projLng = pasajeroLng + qx / cosLat;
            var dist = UtilidadHaversine.DistanciaMetros(pasajeroLat, pasajeroLng, projLat, projLng);

            if (dist < mejorDist)
            {
                mejorDist = dist;
                mejorLat = projLat;
                mejorLng = projLng;
                mejorSeg = i;
            }
        }

        return new ResultadoPuntoEspera
        {
            Lat = mejorLat,
            Lng = mejorLng,
            DistanciaM = Math.Round(mejorDist, 1, MidpointRounding.AwayFromZero),
            SegmentIndex = mejorSeg,
            TooFar = mejorDist > umbralTooFarMetros
        };
    }

    public static IReadOnlyList<(double Lat, double Lng)> ConstruirPuntosRuta(
        string? polilinea,
        IReadOnlyList<(double Lat, double Lng)> waypoints,
        double campusLat,
        double campusLng)
    {
        var decodificados = DecodificadorPolilineaGoogle.Decodificar(polilinea);
        if (decodificados.Count >= 2)
        {
            return decodificados;
        }

        var puntos = new List<(double Lat, double Lng)>();
        if (waypoints is { Count: > 0 })
        {
            puntos.AddRange(waypoints);
        }

        if (puntos.Count == 0
            || Math.Abs(puntos[^1].Lat - campusLat) > 1e-9
            || Math.Abs(puntos[^1].Lng - campusLng) > 1e-9)
        {
            puntos.Add((campusLat, campusLng));
        }

        return puntos;
    }

    private static double ARadianes(double grados) => grados * Math.PI / 180.0;
}
