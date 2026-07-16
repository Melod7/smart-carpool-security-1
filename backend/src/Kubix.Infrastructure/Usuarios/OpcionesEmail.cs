using Microsoft.Extensions.Configuration;

namespace Kubix.Infrastructure.Usuarios;

public sealed class OpcionesEmail
{
    public const string Seccion = "Email";

    [ConfigurationKeyName("SmtpHost")]
    public string ServidorSmtp { get; set; } = "smtp.gmail.com";

    [ConfigurationKeyName("Port")]
    public int Puerto { get; set; } = 587;

    [ConfigurationKeyName("Username")]
    public string Usuario { get; set; } = string.Empty;

    [ConfigurationKeyName("Password")]
    public string Contrasena { get; set; } = string.Empty;

    [ConfigurationKeyName("FromEmail")]
    public string CorreoRemitente { get; set; } = string.Empty;

    [ConfigurationKeyName("FromName")]
    public string NombreRemitente { get; set; } = "Kubix";

    [ConfigurationKeyName("Enabled")]
    public bool Habilitado { get; set; }
}
