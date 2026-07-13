using Kubix.Domain.Enums;

namespace Kubix.Application.Tenancy;

public interface IContextoInquilino
{
    Guid? UniversidadId { get; set; }
    Guid? CampusId { get; set; }
    Guid? UsuarioId { get; set; }
    RolUsuario? Rol { get; set; }
    bool OmitirFiltros { get; set; }
}
