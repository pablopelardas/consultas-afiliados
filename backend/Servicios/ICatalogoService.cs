using ConsultasPlan.Models;

namespace ConsultasPlan.Servicios;

/// <summary>
/// Lectura de los catálogos de referencia.
/// </summary>
/// <remarks>
/// Profesionales y sedes se agrupan en un solo servicio en lugar de tener uno
/// por tabla: son lecturas sin reglas, y un servicio por catálogo serían
/// archivos que no dicen nada.
///
/// Existe de todas formas porque la invariante que importa es que ningún
/// controlador conozca <c>AppDbContext</c>. Dos controladores con acceso
/// directo a la persistencia alcanzan para que la invariante no sea una.
/// </remarks>
public interface ICatalogoService
{
    Task<IReadOnlyList<Profesional>> ListarProfesionales();

    Task<IReadOnlyList<Sede>> ListarSedes();
}
