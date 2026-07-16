namespace Kubix.Application.Usuarios;

public interface IServicioCorreo
{
    Task EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpo,
        CancellationToken ct = default);
}
