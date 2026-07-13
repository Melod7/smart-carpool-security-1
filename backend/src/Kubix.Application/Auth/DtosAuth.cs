using System.Text.Json.Serialization;

namespace Kubix.Application.Auth;

public sealed class SolicitudInicioSesion
{
    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Contrasena { get; set; } = string.Empty;
}

public sealed class SolicitudRefresco
{
    [JsonPropertyName("refreshToken")]
    public string TokenRefresco { get; set; } = string.Empty;
}

public sealed class SolicitudCambioContrasena
{
    [JsonPropertyName("currentPassword")]
    public string ContrasenaActual { get; set; } = string.Empty;

    [JsonPropertyName("newPassword")]
    public string ContrasenaNueva { get; set; } = string.Empty;
}

public sealed class SolicitudCierreSesion
{
    [JsonPropertyName("refreshToken")]
    public string? TokenRefresco { get; set; }
}

public sealed class RespuestaAutenticacion
{
    [JsonPropertyName("accessToken")]
    public string TokenAcceso { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string TokenRefresco { get; set; } = string.Empty;

    [JsonPropertyName("expiresIn")]
    public int ExpiraEnSegundos { get; set; }

    [JsonPropertyName("mustChangePassword")]
    public bool DebeCambiarContrasena { get; set; }

    [JsonPropertyName("user")]
    public ResumenUsuarioDto Usuario { get; set; } = null!;
}

public sealed class ResumenUsuarioDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("universityId")]
    public Guid? UniversidadId { get; set; }

    [JsonPropertyName("campusId")]
    public Guid? CampusId { get; set; }

    [JsonPropertyName("mustChangePassword")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DebeCambiarContrasena { get; set; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Estado { get; set; }
}

public sealed class EstadoSesionUsuario
{
    public Guid UsuarioId { get; init; }
    public string EstadoUsuario { get; init; } = string.Empty;
    public string? EstadoUniversidad { get; init; }
    public bool DebeCambiarContrasena { get; init; }
    public bool Permitido { get; init; }
    public string? MotivoRechazo { get; init; }
}
