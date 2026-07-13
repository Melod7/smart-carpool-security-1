namespace Kubix.Application.Auth;

public sealed class ExcepcionAutenticacion : Exception
{
    public int CodigoEstado { get; }
    public string Titulo { get; }
    public string Codigo { get; }

    public ExcepcionAutenticacion(int codigoEstado, string titulo, string detalle, string codigo)
        : base(detalle)
    {
        CodigoEstado = codigoEstado;
        Titulo = titulo;
        Codigo = codigo;
    }

    public static ExcepcionAutenticacion NoAutorizado(string detalle, string codigo = "unauthorized") =>
        new(StatusCodes.Status401Unauthorized, "Unauthorized", detalle, codigo);

    public static ExcepcionAutenticacion Prohibido(string detalle, string codigo = "forbidden") =>
        new(StatusCodes.Status403Forbidden, "Forbidden", detalle, codigo);

    public static ExcepcionAutenticacion Validacion(string detalle, string codigo = "validation_error") =>
        new(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", detalle, codigo);
}

/// <summary>
/// Constantes de códigos HTTP sin depender de ASP.NET Core en Application.
/// </summary>
file static class StatusCodes
{
    public const int Status401Unauthorized = 401;
    public const int Status403Forbidden = 403;
    public const int Status422UnprocessableEntity = 422;
}
