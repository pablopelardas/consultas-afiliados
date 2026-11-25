using ConsultasPlan.Dominio.Cupo;
using ConsultasPlan.Models;

namespace ConsultasPlan.Contratos;

/// <summary>
/// Contratos de salida de la API.
/// </summary>
/// <remarks>
/// El código base devolvía las entidades de EF. Eso tiene tres consecuencias:
/// expone campos internos (<c>Activo</c>, <c>FechaBaja</c>, <c>SedeId</c>),
/// ata el contrato público al esquema —cualquier cambio del modelo rompe a los
/// clientes— y arrastra el grafo de navegación completo, que fue lo que obligó
/// a configurar <c>ReferenceHandler.IgnoreCycles</c> para poder serializar.
///
/// Con contratos explícitos esa configuración deja de ser necesaria: no hay
/// ciclos porque el DTO no los tiene.
/// </remarks>
public record AfiliadoResponse(
    int Id,
    string NombreCompleto,
    string DNI,
    string Email,
    string Telefono,
    string Plan,
    int ConsultasUsadas,
    bool Bloqueado,
    DateTime FechaAlta)
{
    public static AfiliadoResponse De(Afiliado a) => new(
        a.Id, a.NombreCompleto, a.DNI, a.Email, a.Telefono,
        a.Plan, a.ConsultasUsadas, a.Bloqueado, a.FechaAlta);
}

/// <summary>
/// Afiliado embebido en una consulta: solo lo necesario para identificarlo.
/// </summary>
public record AfiliadoEnConsulta(int Id, string NombreCompleto, string DNI)
{
    public static AfiliadoEnConsulta? De(Afiliado? a) =>
        a is null ? null : new AfiliadoEnConsulta(a.Id, a.NombreCompleto, a.DNI);
}

/// <summary>
/// La especialidad se expone por su nombre, igual que antes del catálogo: el
/// frontend sigue leyendo <c>especialidad</c> como texto y no se rompe. El
/// código es identidad interna y no forma parte del contrato público.
/// </summary>
public record ProfesionalResponse(
    int Id,
    string NombreCompleto,
    string Especialidad,
    SedeResponse? Sede)
{
    public static ProfesionalResponse De(Profesional p) => new(
        p.Id, p.NombreCompleto, p.Especialidad?.Nombre ?? string.Empty, SedeResponse.De(p.Sede));
}

public record ProfesionalEnConsulta(int Id, string NombreCompleto, string Especialidad)
{
    public static ProfesionalEnConsulta? De(Profesional? p) =>
        p is null ? null : new ProfesionalEnConsulta(
            p.Id, p.NombreCompleto, p.Especialidad?.Nombre ?? string.Empty);
}

public record SedeResponse(int Id, string Nombre, string Direccion)
{
    public static SedeResponse? De(Sede? s) =>
        s is null ? null : new SedeResponse(s.Id, s.Nombre, s.Direccion);
}

/// <summary>
/// Cupo de un período. <c>Hasta</c> es el último día incluido.
/// </summary>
/// <remarks>
/// El dominio trabaja con intervalos semiabiertos, pero un contrato de salida es
/// para leerlo: "hasta el 31/12" se entiende y "hasta el 01/01" se malinterpreta.
/// </remarks>
public record CupoPeriodoResponse(
    int Limite, int Usadas, int Disponibles, DateTime Desde, DateTime Hasta)
{
    public static CupoPeriodoResponse De(int limite, int usadas, int disponibles, Periodo periodo) =>
        new(limite, usadas, disponibles, periodo.Desde, periodo.UltimoDia);
}

public record CupoNutricionResponse(CupoPeriodoResponse Ano, CupoPeriodoResponse Mes)
{
    public static CupoNutricionResponse De(EstadoDelCupo e) => new(
        CupoPeriodoResponse.De(e.LimiteAnual, e.UsadasEnElAno, e.DisponiblesEnElAno, e.Anual),
        CupoPeriodoResponse.De(e.LimiteMensual, e.UsadasEnElMes, e.DisponiblesEnElMes, e.Mensual));
}

public record ConsultaResponse(
    int Id,
    AfiliadoEnConsulta? Afiliado,
    ProfesionalEnConsulta? Profesional,
    DateTime FechaHora,
    EstadoConsulta Estado,
    ModalidadConsulta Modalidad,
    string Motivo,
    DateTime FechaCreacion)
{
    public static ConsultaResponse De(Consulta c) => new(
        c.Id,
        AfiliadoEnConsulta.De(c.Afiliado),
        ProfesionalEnConsulta.De(c.Profesional),
        c.FechaHora, c.Estado, c.Modalidad, c.Motivo, c.FechaCreacion);
}
