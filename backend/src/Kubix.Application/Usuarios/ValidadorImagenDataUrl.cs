using System.Text.RegularExpressions;

namespace Kubix.Application.Usuarios;

public static partial class ValidadorImagenDataUrl
{
    public const int MaximoBytes = 2 * 1024 * 1024;

    public static bool EsValida(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var coincidencia = PatronDataUrl().Match(valor.Trim());
        if (!coincidencia.Success)
        {
            return false;
        }

        var base64 = coincidencia.Groups["data"].Value;
        if (base64.Length > ((MaximoBytes + 2) / 3) * 4)
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(base64).Length <= MaximoBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [GeneratedRegex(
        @"^data:image/(?:jpeg|png|webp);base64,(?<data>[A-Za-z0-9+/]+={0,2})$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PatronDataUrl();
}
