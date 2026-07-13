using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kubix.Application.Auth;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kubix.Infrastructure.Auth;

public sealed class ServicioTokenJwt(IOptions<OpcionesJwt> opciones) : IServicioTokenJwt
{
    private readonly OpcionesJwt _opciones = opciones.Value;

    public int MinutosExpiracionAcceso => _opciones.AccessTokenMinutes;

    public string CrearTokenAcceso(Usuario usuario)
    {
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Key));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);
        var ahora = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Correo),
            new("role", ConversorEnumDominio.ACadenaDb(usuario.Rol)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (usuario.UniversidadId is Guid universidadId)
        {
            claims.Add(new Claim("university_id", universidadId.ToString()));
        }

        if (usuario.CampusId is Guid campusId)
        {
            claims.Add(new Claim("campus_id", campusId.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _opciones.Issuer,
            audience: _opciones.Audience,
            claims: claims,
            notBefore: ahora,
            expires: ahora.AddMinutes(_opciones.AccessTokenMinutes),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
