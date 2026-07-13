using Kubix.Application.Tenancy;
using Kubix.Domain.Enums;

namespace Kubix.Infrastructure.Tenancy;

public sealed class ContextoInquilino : IContextoInquilino
{
    public Guid? UniversidadId { get; set; }
    public Guid? CampusId { get; set; }
    public Guid? UsuarioId { get; set; }
    public RolUsuario? Rol { get; set; }

    /// <summary>
    /// Por defecto true para seed/migrate/health y requests no autenticadas.
    /// El middleware lo ajusta según el JWT.
    /// </summary>
    public bool OmitirFiltros { get; set; } = true;
}
