using System.Globalization;

namespace ConsultasPlan.Dominio.Cupo;

/// <summary>
/// Intervalo de tiempo semiabierto <c>[Desde, Hasta)</c>.
/// </summary>
/// <remarks>
/// Semiabierto y no cerrado: con intervalos cerrados hay que elegir el último
/// instante representable del período, y ese instante depende de la precisión
/// con la que la base guarda la fecha. Con el extremo abierto, el límite es el
/// primer instante del período siguiente y los períodos consecutivos no se
/// solapan ni dejan huecos, cualquiera sea la precisión.
/// </remarks>
public readonly record struct Periodo(DateTime Desde, DateTime Hasta)
{
    public bool Contiene(DateTime instante) => instante >= Desde && instante < Hasta;

    /// <summary>Último día del período, para mostrarle al usuario.</summary>
    public DateTime UltimoDia => Hasta.AddDays(-1);

    // Fechas en formato fijo y cultura invariante: el mensaje de error es parte
    // del contrato de la API y no debe cambiar según la cultura del servidor.
    public string DesdeTexto => Desde.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public string UltimoDiaTexto => UltimoDia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public string ProximoInicioTexto => Hasta.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}

/// <summary>
/// Los dos períodos contra los que se mide el cupo de una consulta.
/// </summary>
public readonly record struct PeriodosDelCupo(Periodo Anual, Periodo Mensual);
