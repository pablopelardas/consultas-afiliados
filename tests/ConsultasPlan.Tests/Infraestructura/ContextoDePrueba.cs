using ConsultasPlan.Data;
using ConsultasPlan.Models;
using Microsoft.EntityFrameworkCore;

namespace ConsultasPlan.Tests.Infraestructura;

/// <summary>
/// Contexto EF en memoria con datos mínimos y controlados.
/// </summary>
/// <remarks>
/// No se reutiliza <see cref="DbSeeder"/>: sus fechas se derivan de
/// <c>DateTime.Now</c>, así que los datos cambian según el día en que corran
/// los tests. Un test cuyo resultado depende de la fecha de ejecución no
/// verifica nada.
/// </remarks>
public static class ContextoDePrueba
{
    public static AppDbContext Crear()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(opciones);
    }

    public static Afiliado AgregarAfiliado(
        this AppDbContext db,
        string nombre = "Afiliado de Prueba",
        bool bloqueado = false)
    {
        var afiliado = new Afiliado
        {
            NombreCompleto = nombre,
            DNI = "30000000",
            Email = "afiliado@test.local",
            Telefono = "1100000000",
            Plan = "Plan Integral",
            Bloqueado = bloqueado,
            FechaAlta = new DateTime(2024, 1, 1),
            Activo = true
        };

        db.Afiliados.Add(afiliado);
        db.SaveChanges();
        return afiliado;
    }

    public static Profesional AgregarProfesional(
        this AppDbContext db,
        string especialidad = "Nutrición")
    {
        var sede = new Sede { Nombre = "Sede de Prueba", Direccion = "Calle 1" };
        db.Sedes.Add(sede);
        db.SaveChanges();

        var profesional = new Profesional
        {
            NombreCompleto = "Profesional de Prueba",
            Especialidad = especialidad,
            SedeId = sede.Id
        };

        db.Profesionales.Add(profesional);
        db.SaveChanges();
        return profesional;
    }

    public static Consulta AgregarConsulta(
        this AppDbContext db,
        Afiliado afiliado,
        Profesional profesional,
        DateTime fechaHora,
        EstadoConsulta estado = EstadoConsulta.Pendiente,
        ModalidadConsulta modalidad = ModalidadConsulta.Presencial)
    {
        var consulta = new Consulta
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = fechaHora,
            Estado = estado,
            Modalidad = modalidad,
            FechaCreacion = fechaHora.AddDays(-5),
            Motivo = "Motivo de prueba"
        };

        db.Consultas.Add(consulta);
        db.SaveChanges();
        return consulta;
    }
}
