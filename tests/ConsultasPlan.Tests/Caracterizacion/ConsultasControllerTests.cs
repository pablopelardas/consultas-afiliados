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
    // Problema #4 — La ventana de cancelación está mal calculada.
    // El mensaje al usuario dice 24 horas; el código valida 23.
    [Fact]
    public async Task Cancelar_a_23_horas_y_media_de_la_consulta_es_aceptado_aunque_el_mensaje_diga_24_horas()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var consulta = db.AgregarConsulta(
            afiliado, profesional, DateTime.Now.AddHours(23).AddMinutes(30));

        var controller = new ConsultasController(db);

        var resultado = await controller.CancelarConsulta(consulta.Id);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(EstadoConsulta.Cancelado, db.Consultas.Find(consulta.Id)!.Estado);
    }

    // Problema #6 — No hay máquina de estados.
    // Una consulta ya cancelada acepta que se le registre una inasistencia.
    [Fact]
    public async Task Marcar_inasistencia_sobre_una_consulta_cancelada_es_aceptado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var consulta = db.AgregarConsulta(
            afiliado, profesional, DateTime.Now.AddDays(-10), EstadoConsulta.Cancelado);

        var controller = new ConsultasController(db);

        var resultado = await controller.MarcarInasistencia(consulta.Id);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(EstadoConsulta.Inasistencia, db.Consultas.Find(consulta.Id)!.Estado);
    }

    // Problema #6 — Misma causa: se puede registrar la atención de una consulta
    // que fue cancelada.
    [Fact]
    public async Task Registrar_atencion_sobre_una_consulta_cancelada_es_aceptado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var consulta = db.AgregarConsulta(
            afiliado, profesional, new DateTime(DateTime.Now.Year, 3, 10), EstadoConsulta.Cancelado);

        var controller = new ConsultasController(db);

        var resultado = await controller.RegistrarAtencion(consulta.Id);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(EstadoConsulta.Atendido, db.Consultas.Find(consulta.Id)!.Estado);
    }

    // Problema #5 — PUT /{id}/estado no valida nada.
    // Permite pasar de Atendido a Pendiente, una transición que no existe en el
    // dominio, y saltea toda regla de negocio.
    [Fact]
    public async Task Actualizar_estado_permite_volver_de_Atendido_a_Pendiente()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var consulta = db.AgregarConsulta(
            afiliado, profesional, DateTime.Now.AddDays(-10), EstadoConsulta.Atendido);

        var controller = new ConsultasController(db);

        var resultado = await controller.ActualizarEstado(
            consulta.Id, new ActualizarEstadoRequest { Estado = EstadoConsulta.Pendiente });

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(EstadoConsulta.Pendiente, db.Consultas.Find(consulta.Id)!.Estado);
    }

    // Problema #5 — Tampoco valida que el valor recibido sea un miembro del
    // enum. Un entero fuera de rango se persiste igual.
    [Fact]
    public async Task Actualizar_estado_acepta_un_valor_que_no_pertenece_al_enum()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();
        var consulta = db.AgregarConsulta(afiliado, profesional, DateTime.Now.AddDays(5));

        var controller = new ConsultasController(db);
        var estadoInexistente = (EstadoConsulta)99;

        var resultado = await controller.ActualizarEstado(
            consulta.Id, new ActualizarEstadoRequest { Estado = estadoInexistente });

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(estadoInexistente, db.Consultas.Find(consulta.Id)!.Estado);
        Assert.False(Enum.IsDefined(db.Consultas.Find(consulta.Id)!.Estado));
    }

    // Problema #9 — No se valida que la fecha solicitada sea futura.
    // Se puede agendar una consulta en el pasado.
    [Fact]
    public async Task Crear_consulta_con_fecha_en_el_pasado_es_aceptado()
    {
        using var db = ContextoDePrueba.Crear();
        var afiliado = db.AgregarAfiliado();
        var profesional = db.AgregarProfesional();

        var controller = new ConsultasController(db);

        var resultado = await controller.CrearConsulta(new Consulta
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = new DateTime(1990, 1, 1),
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
        var ocupado = new DateTime(DateTime.Now.Year + 1, 6, 10, 10, 0, 0);
        db.AgregarConsulta(afiliado, profesional, ocupado, EstadoConsulta.Confirmado);

        var controller = new ConsultasController(db);

        var resultado = await controller.CrearConsulta(new Consulta
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

        var controller = new ConsultasController(db);

        await controller.CrearConsulta(new Consulta
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = profesional.Id,
            FechaHora = DateTime.Now.AddDays(10),
            Modalidad = ModalidadConsulta.Presencial,
            Motivo = "Con afiliado anidado",
            Afiliado = new Afiliado { NombreCompleto = "Inyectado", DNI = "99999999" }
        });

        Assert.Equal(afiliadosAntes + 1, db.Afiliados.Count());
    }
}
