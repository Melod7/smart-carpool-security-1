using System.Text.Json.Serialization;

namespace Kubix.Application.Usuarios;

public sealed class SolicitudRegistroDto
{
    [JsonPropertyName("universityId")]
    public Guid UniversidadId { get; set; }

    [JsonPropertyName("campusId")]
    public Guid CampusId { get; set; }

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Contrasena { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string? Genero { get; set; }

    [JsonPropertyName("profileImage")]
    public string? ImagenPerfil { get; set; }

    [JsonPropertyName("career")]
    public string? Carrera { get; set; }

    [JsonPropertyName("idNumber")]
    public string? NumeroIdentificacion { get; set; }

    [JsonPropertyName("vehicle")]
    public VehiculoRegistroDto? Vehiculo { get; set; }
}

public sealed class VehiculoRegistroDto
{
    [JsonPropertyName("makeModel")]
    public string MarcaModelo { get; set; } = string.Empty;

    [JsonPropertyName("plate")]
    public string Placa { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("seatsTotal")]
    public int AsientosTotales { get; set; }

    [JsonPropertyName("image")]
    public string? Imagen { get; set; }
}

public sealed class RespuestaRegistroDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;
}

public sealed class UniversidadPublicaDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("campuses")]
    public IReadOnlyList<CampusPublicoDto> Campuses { get; set; } = Array.Empty<CampusPublicoDto>();
}

public sealed class CampusPublicoDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;
}

public sealed class SolicitudRegistroResumenDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string? Genero { get; set; }

    [JsonPropertyName("profileImage")]
    public string? ImagenPerfil { get; set; }

    [JsonPropertyName("career")]
    public string? Carrera { get; set; }

    [JsonPropertyName("idNumber")]
    public string? NumeroIdentificacion { get; set; }

    [JsonPropertyName("campusId")]
    public Guid CampusId { get; set; }

    [JsonPropertyName("campusName")]
    public string? NombreCampus { get; set; }

    [JsonPropertyName("vehicleJson")]
    public string? VehiculoJson { get; set; }

    /// <summary>registration | vehicle_change | role_change_driver | profile_change</summary>
    [JsonPropertyName("kind")]
    public string Tipo { get; set; } = "registration";

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class UsuarioAdminDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string? Genero { get; set; }

    [JsonPropertyName("profileImage")]
    public string? ImagenPerfil { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("campusId")]
    public Guid? CampusId { get; set; }

    [JsonPropertyName("career")]
    public string? Carrera { get; set; }

    [JsonPropertyName("idNumber")]
    public string? NumeroIdentificacion { get; set; }

    [JsonPropertyName("ratingAvg")]
    public decimal PromedioCalificacion { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class FiltroUsuariosAdmin
{
    public string? Estado { get; set; }
    public string? Rol { get; set; }
    public Guid? CampusId { get; set; }
    public string? Busqueda { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanoPagina { get; set; } = 20;
}

public sealed class PaginaUsuariosAdmin
{
    public IReadOnlyList<UsuarioAdminDto> Items { get; set; } = Array.Empty<UsuarioAdminDto>();
    public int Total { get; set; }
}

public sealed class PerfilUsuarioDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string? Genero { get; set; }

    [JsonPropertyName("profileImage")]
    public string? ImagenPerfil { get; set; }

    [JsonPropertyName("career")]
    public string? Carrera { get; set; }

    [JsonPropertyName("campusId")]
    public Guid? CampusId { get; set; }

    [JsonPropertyName("universityId")]
    public Guid? UniversidadId { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;
}

public sealed class SolicitudActualizarPerfil
{
    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("career")]
    public string? Carrera { get; set; }

    [JsonPropertyName("gender")]
    public string? Genero { get; set; }

    [JsonPropertyName("profileImage")]
    public string? ImagenPerfil { get; set; }
}

public sealed class RespuestaCambioPerfilDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = "pending";

    [JsonPropertyName("kind")]
    public string Tipo { get; set; } = "profile_change";

    [JsonPropertyName("message")]
    public string Mensaje { get; set; } = string.Empty;
}

public sealed class SolicitudCambioModo
{
    [JsonPropertyName("mode")]
    public string Modo { get; set; } = string.Empty;

    [JsonPropertyName("vehicle")]
    public VehiculoRegistroDto? Vehiculo { get; set; }
}

public sealed class RespuestaCambioModoDto
{
    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? SolicitudId { get; set; }

    [JsonPropertyName("requiresReauthentication")]
    public bool RequiereReautenticacion { get; set; }

    [JsonPropertyName("message")]
    public string Mensaje { get; set; } = string.Empty;
}

public sealed class ContactoEmergenciaDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("relationship")]
    public string Relacion { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string Telefono { get; set; } = string.Empty;
}

public sealed class SolicitudCrearContactoEmergencia
{
    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("relationship")]
    public string Relacion { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string Telefono { get; set; } = string.Empty;
}
