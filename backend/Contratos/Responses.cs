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

public record ProfesionalResponse(
    int Id,
    string NombreCompleto,
    string Especialidad,
    SedeResponse? Sede)
{
    public static ProfesionalResponse De(Profesional p) => new(
        p.Id, p.NombreCompleto, p.Especialidad, SedeResponse.De(p.Sede));
}

public record ProfesionalEnConsulta(int Id, string NombreCompleto, string Especialidad)
{
    public static ProfesionalEnConsulta? De(Profesional? p) =>
        p is null ? null : new ProfesionalEnConsulta(p.Id, p.NombreCompleto, p.Especialidad);
}

public record SedeResponse(int Id, string Nombre, string Direccion)
{
    public static SedeResponse? De(Sede? s) =>
        s is null ? null : new SedeResponse(s.Id, s.Nombre, s.Direccion);
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
