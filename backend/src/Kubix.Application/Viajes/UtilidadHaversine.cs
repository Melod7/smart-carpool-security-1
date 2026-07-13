namespace Kubix.Application.Viajes;

/// <summary>
/// Distancia en línea recta entre dos coordenadas (km).
/// </summary>
public static class UtilidadHaversine
{
    private const double RadioTierraKm = 6371.0;

    public static decimal DistanciaKm(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ARadianes(lat2 - lat1);
        var dLng = ARadianes(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ARadianes(lat1)) * Math.Cos(ARadianes(lat2))
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round((decimal)(RadioTierraKm * c), 3, MidpointRounding.AwayFromZero);
    }

    private static double ARadianes(double grados) => grados * Math.PI / 180.0;
}
