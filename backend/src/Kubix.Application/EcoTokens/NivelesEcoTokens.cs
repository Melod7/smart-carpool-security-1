namespace Kubix.Application.EcoTokens;

/// <summary>
/// Umbrales y fórmula de progreso sobre eco_lifetime.
/// Bronce 0 / Plata 100 / Oro 500 / Platino 2000; progress null en Platino.
/// </summary>
public static class NivelesEcoTokens
{
    public const int UmbralPlata = 100;
    public const int UmbralOro = 500;
    public const int UmbralPlatino = 2000;

    public static (string Level, double? Progress) Calcular(int ecoVitalicio)
    {
        if (ecoVitalicio >= UmbralPlatino)
        {
            return ("Platino", null);
        }

        if (ecoVitalicio >= UmbralOro)
        {
            return ("Oro", ProgressEntre(ecoVitalicio, UmbralOro, UmbralPlatino));
        }

        if (ecoVitalicio >= UmbralPlata)
        {
            return ("Plata", ProgressEntre(ecoVitalicio, UmbralPlata, UmbralOro));
        }

        return ("Bronce", ProgressEntre(ecoVitalicio, 0, UmbralPlata));
    }

    private static double ProgressEntre(int vitalicio, int piso, int techo) =>
        (vitalicio - piso) / (double)(techo - piso);
}
