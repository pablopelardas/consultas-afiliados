namespace ConsultasPlan.Models;

/// <summary>
/// Especialidad médica del catálogo.
/// </summary>
/// <remarks>
/// <see cref="Codigo"/> es identidad y <see cref="Nombre"/> es presentación. La
/// política de cupo compara contra el código, nunca contra el nombre: el nombre
/// es editable desde un ABM, y si la regla dependiera de él, renombrar
/// "Nutrición" a "Nutrición y Dietética" desactivaría la política en silencio.
///
/// Tampoco compara contra <see cref="Id"/>: una clave subrogada depende del
/// orden en que se poblaron las filas, que difiere entre ambientes.
///
/// Es la separación que usan los nomencladores de salud (CIE-10, SNOMED).
/// </remarks>
public class Especialidad
{
    public int Id { get; set; }

    /// <summary>Identidad estable. Inmutable por contrato, con índice único.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Lo que muestra la interfaz.</summary>
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>
/// Códigos del catálogo que el dominio referencia por nombre propio.
/// </summary>
public static class CodigosDeEspecialidad
{
    public const string Nutricion = "NUT";
}
