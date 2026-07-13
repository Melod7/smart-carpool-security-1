namespace Kubix.Infrastructure.Auth;

public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";

    public string Issuer { get; set; } = "kubix";
    public string Audience { get; set; } = "kubix";
    public string Key { get; set; } = "dev-only-change-me-to-a-long-secret-key-32+";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
}
