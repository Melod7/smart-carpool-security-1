namespace Kubix.Application.Viajes;

/// <summary>
/// Distancia en línea recta entre dos coordenadas.
/// </summary>
public static class UtilidadHaversine
{
    private const double RadioTierraKm = 6371.0;
    private const double RadioTierraM = 6_371_000.0;

    public static decimal DistanciaKm(double lat1, double lng1, double lat2, double lng2) =>
        Math.Round((decimal)(DistanciaMetros(lat1, lng1, lat2, lng2) / 1000.0), 3, MidpointRounding.AwayFromZero);

    public static double DistanciaMetros(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ARadianes(lat2 - lat1);
        var dLng = ARadianes(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ARadianes(lat1)) * Math.Cos(ARadianes(lat2))
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return RadioTierraM * c;
    }

    /// <summary>
    /// Suma haversine a lo largo de una secuencia de puntos (km, redondeado a 3 decimales).
    /// </summary>
    public static decimal DistanciaALoLargoKm(IReadOnlyList<(double Lat, double Lng)> puntos)
    {
        if (puntos.Count < 2)
        {
            return 0m;
        }

        double metros = 0;
        for (var i = 0; i < puntos.Count - 1; i++)
        {
            metros += DistanciaMetros(
                puntos[i].Lat,
                puntos[i].Lng,
                puntos[i + 1].Lat,
                puntos[i + 1].Lng);
        }

        return Math.Round((decimal)(metros / 1000.0), 3, MidpointRounding.AwayFromZero);
    }

    private static double ARadianes(double grados) => grados * Math.PI / 180.0;
}
