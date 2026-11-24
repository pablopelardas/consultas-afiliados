using ConsultasPlan.Controllers;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Tests.Caracterizacion;

/// <summary>
/// Tests de caracterización: documentan el comportamiento ACTUAL del código
/// base, incluido el incorrecto.
/// </summary>
/// <remarks>
/// Un test de caracterización no afirma que el comportamiento sea correcto:
/// afirma que es el que hay. Sirve como red antes de refactorizar y como
/// evidencia reproducible de cada defecto. Cuando la rama de corrección
/// correspondiente cambie la conducta, estos tests fallarán — y ese fallo es
/// la confirmación de que el fix hizo efecto.
///
/// Cada test referencia el número de problema del inventario en
/// docs/DOCUMENTO-TECNICO.md §4.
/// </remarks>
public class ConsultasControllerTests
{
    private static ConsultasController Controlador(ConsultasPlan.Data.AppDbContext db) =>
        new(db, Reloj.Fijo());

    // Problema #9 — No se valida que la fecha solicitada sea futura.
    // Se puede agendar una consulta en el pasado.
    [Fact]
    public async Task Crear_consulta_con_fecha_en_el_pasado_es_aceptado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();

        var resultado = await Controlador(db).CrearConsulta(new Consulta
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = Reloj.Ahora.AddYears(-30),
            Modalidad = ModalidadConsulta.Presencial,
            Motivo = "Consulta en el pasado"
        });

        Assert.IsType<CreatedAtActionResult>(resultado);
    }

    // Problema #10 — La detección de superposición compara igualdad exacta de
    // FechaHora. Un minuto de diferencia sobre el mismo profesional pasa.
    [Fact]
    public async Task Crear_consulta_un_minuto_despues_de_otra_del_mismo_profesional_es_aceptado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var ocupado = Reloj.Ahora.AddDays(30);
        db.AgregarConsulta(afiliado, profesional, ocupado, EstadoConsulta.Confirmado);

        var resultado = await Controlador(db).CrearConsulta(new Consulta
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = ocupado.AddMinutes(1),
            Modalidad = ModalidadConsulta.Presencial,
            Motivo = "Se superpone con la anterior"
        });

        Assert.IsType<CreatedAtActionResult>(resultado);
    }

    // Problema #8 — Over-posting. El endpoint recibe la entidad de EF, así que
    // el cliente puede enviar un Afiliado anidado y EF lo persiste como un
    // afiliado nuevo, sin que nadie lo haya pedido.
    [Fact]
    public async Task Crear_consulta_con_un_afiliado_anidado_persiste_un_afiliado_nuevo()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var afiliadosAntes = db.Afiliados.Count();

        await Controlador(db).CrearConsulta(new Consulta
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = Reloj.Ahora.AddDays(10),
            Modalidad = ModalidadConsulta.Presencial,
            Motivo = "Con afiliado anidado",
            Afiliado = new Afiliado { NombreCompleto = "Inyectado", DNI = "99999999" }
        });

        Assert.Equal(afiliadosAntes + 1, db.Afiliados.Count());
    }
}
