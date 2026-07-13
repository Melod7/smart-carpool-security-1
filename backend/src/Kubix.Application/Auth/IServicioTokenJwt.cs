using Kubix.Domain.Entities;

namespace Kubix.Application.Auth;

public interface IServicioTokenJwt
{
    string CrearTokenAcceso(Usuario usuario);
    int MinutosExpiracionAcceso { get; }
}
