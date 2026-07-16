using System.Net;
using System.Net.Mail;
using Kubix.Application.Usuarios;
using Microsoft.Extensions.Options;

namespace Kubix.Infrastructure.Usuarios;

public sealed class ServicioCorreoSmtp(IOptions<OpcionesEmail> opciones) : IServicioCorreo
{
    private readonly OpcionesEmail _opciones = opciones.Value;

    public async Task EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpo,
        CancellationToken ct = default)
    {
        if (!_opciones.Habilitado)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_opciones.Usuario)
            || string.IsNullOrWhiteSpace(_opciones.Contrasena)
            || string.IsNullOrWhiteSpace(_opciones.CorreoRemitente))
        {
            throw new InvalidOperationException(
                "Email SMTP is enabled but Username, Password or FromEmail is missing.");
        }

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_opciones.CorreoRemitente, _opciones.NombreRemitente),
            Subject = asunto,
            Body = cuerpo,
            IsBodyHtml = false
        };
        mensaje.To.Add(destinatario);

        using var cliente = new SmtpClient(_opciones.ServidorSmtp, _opciones.Puerto)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_opciones.Usuario, _opciones.Contrasena)
        };

        ct.ThrowIfCancellationRequested();
        await cliente.SendMailAsync(mensaje, ct);
    }
}
