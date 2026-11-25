namespace ConsultasPlan.Servicios;

/// <summary>
/// Motivo por el que una operación de dominio no se pudo completar.
/// </summary>
/// <remarks>
/// El servicio nombra el motivo en términos del dominio. Traducirlo a un código
/// HTTP es responsabilidad del controlador: la capa de servicios no conoce HTTP
/// y por eso puede usarse desde un job, un comando de consola o un test sin
/// pipeline.
/// </remarks>
public enum TipoDeFalla
{
    /// <summary>El recurso no existe, o no es visible para la operación pedida.</summary>
    NoEncontrado,

    /// <summary>La operación es válida pero el estado actual no la permite.</summary>
    Conflicto,

    /// <summary>Los datos recibidos no son utilizables.</summary>
    EntradaInvalida
}

public sealed record Falla(TipoDeFalla Tipo, string Detalle, string? Campo = null)
{
    public static Falla NoEncontrado(string detalle) =>
        new(TipoDeFalla.NoEncontrado, detalle);

    public static Falla Conflicto(string detalle) =>
        new(TipoDeFalla.Conflicto, detalle);

    public static Falla EntradaInvalida(string campo, string detalle) =>
        new(TipoDeFalla.EntradaInvalida, detalle, campo);
}

/// <summary>
/// Resultado de una operación de dominio que no devuelve valor.
/// </summary>
public class ServiceResult
{
    public Falla? Falla { get; }

    public bool EsExito => Falla is null;

    protected ServiceResult(Falla? falla) => Falla = falla;

    public static ServiceResult Ok() => new(null);

    public static ServiceResult<T> Ok<T>(T valor) => ServiceResult<T>.Exitoso(valor);

    // La conversión implícita permite escribir `return Falla.Conflicto(...)` sin
    // envolver nada, lo que mantiene las guardas del servicio en una línea.
    public static implicit operator ServiceResult(Falla falla) => new(falla);
}

/// <summary>
/// Resultado de una operación de dominio que devuelve valor.
/// </summary>
public sealed class ServiceResult<T> : ServiceResult
{
    private readonly T? _valor;

    private ServiceResult(T? valor, Falla? falla) : base(falla) => _valor = valor;

    internal static ServiceResult<T> Exitoso(T valor) => new(valor, null);

    /// <summary>
    /// Solo hay valor cuando la operación tuvo éxito. Leerlo sobre una falla es
    /// un error de programación, no un caso a manejar: por eso lanza en lugar
    /// de devolver <c>null</c> y dejar que el fallo aparezca más adelante.
    /// </summary>
    public T Valor => EsExito
        ? _valor!
        : throw new InvalidOperationException(
            $"La operación no tuvo éxito ({Falla!.Tipo}): no hay valor que leer.");

    public static implicit operator ServiceResult<T>(Falla falla) => new(default, falla);
}
