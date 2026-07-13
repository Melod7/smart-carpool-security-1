namespace Kubix.Application.Usuarios;

public sealed class ExcepcionRegistroUsuarios : Exception
{
    public int CodigoEstado { get; }
    public string Titulo { get; }
    public string Codigo { get; }

    public ExcepcionRegistroUsuarios(int codigoEstado, string titulo, string detalle, string codigo)
        : base(detalle)
    {
        CodigoEstado = codigoEstado;
        Titulo = titulo;
        Codigo = codigo;
    }

    public static ExcepcionRegistroUsuarios NoEncontrado(string detalle, string codigo = "not_found") =>
        new(StatusCodes.Status404NotFound, "Not Found", detalle, codigo);

    public static ExcepcionRegistroUsuarios Conflicto(string detalle, string codigo = "conflict") =>
        new(StatusCodes.Status409Conflict, "Conflict", detalle, codigo);

    public static ExcepcionRegistroUsuarios Validacion(string detalle, string codigo = "validation_error") =>
        new(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", detalle, codigo);

    public static ExcepcionRegistroUsuarios NoAutorizado(string detalle, string codigo = "unauthorized") =>
        new(StatusCodes.Status401Unauthorized, "Unauthorized", detalle, codigo);
}

file static class StatusCodes
{
    public const int Status401Unauthorized = 401;
    public const int Status404NotFound = 404;
    public const int Status409Conflict = 409;
    public const int Status422UnprocessableEntity = 422;
}
