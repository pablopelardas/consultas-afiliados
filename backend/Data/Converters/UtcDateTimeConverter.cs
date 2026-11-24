using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ConsultasPlan.Data.Converters;

/// <summary>
/// Garantiza que todo <see cref="DateTime"/> se persista en UTC y se lea con
/// <see cref="DateTimeKind.Utc"/>.
/// </summary>
/// <remarks>
/// SQLite no tiene tipo fecha: guarda texto sin zona horaria. EF Core devuelve
/// esos valores con <see cref="DateTimeKind.Unspecified"/>, y .NET trata lo
/// Unspecified como hora local al convertir. El resultado es que un instante
/// guardado en UTC se reinterpreta como local al leerlo, con un desvío igual al
/// huso del servidor —tres horas en Argentina— que no produce ningún error
/// visible.
///
/// Un <see cref="DateTime"/> con Kind Unspecified se asume UTC en lugar de
/// convertirse desde la hora local: así el instante almacenado no depende del
/// huso del servidor que recibió la petición. Lo estricto sería rechazar fechas
/// sin zona en el borde de la API; queda registrado en el documento técnico.
/// </remarks>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        aLaBase => Normalizar(aLaBase),
        desdeLaBase => DateTime.SpecifyKind(desdeLaBase, DateTimeKind.Utc))
    {
    }

    private static DateTime Normalizar(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Utc => valor,
        DateTimeKind.Local => valor.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc)
    };
}

/// <inheritdoc cref="UtcDateTimeConverter"/>
public class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableDateTimeConverter() : base(
        aLaBase => aLaBase.HasValue ? Normalizar(aLaBase.Value) : null,
        desdeLaBase => desdeLaBase.HasValue
            ? DateTime.SpecifyKind(desdeLaBase.Value, DateTimeKind.Utc)
            : null)
    {
    }

    private static DateTime Normalizar(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Utc => valor,
        DateTimeKind.Local => valor.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc)
    };
}
