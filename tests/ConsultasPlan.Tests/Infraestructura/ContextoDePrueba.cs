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
        bool bloqueado = false,
        string dni = "30000000")
    {
        var afiliado = new Afiliado
        {
            NombreCompleto = nombre,
            DNI = dni,
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

    /// <summary>
    /// Especialidad del catálogo, creada solo si falta.
    /// </summary>
    /// <remarks>
    /// El proveedor InMemory no aplica el <c>HasData</c> del catálogo si nadie
    /// crea la base, así que los tests lo construyen a demanda.
    /// </remarks>
    public static Especialidad AgregarEspecialidad(this AppDbContext db, string codigo)
    {
        var existente = db.Especialidades.FirstOrDefault(e => e.Codigo == codigo);
        if (existente is not null) return existente;

        var especialidad = new Especialidad { Codigo = codigo, Nombre = $"Especialidad {codigo}" };
        db.Especialidades.Add(especialidad);
        db.SaveChanges();
        return especialidad;
    }

    /// <remarks>
    /// El valor por defecto NO es nutrición a propósito: un test entra en la
    /// política de cupo solo si la pide explícitamente, así que agregar la
    /// política no cambia el significado de los tests que ya existían.
    /// </remarks>
    public static Profesional AgregarProfesional(
        this AppDbContext db,
        string codigoEspecialidad = "CLI")
    {
        var sede = new Sede { Nombre = "Sede de Prueba", Direccion = "Calle 1" };
        db.Sedes.Add(sede);
        db.SaveChanges();

        var profesional = new Profesional
        {
            NombreCompleto = "Profesional de Prueba",
            EspecialidadId = db.AgregarEspecialidad(codigoEspecialidad).Id,
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
