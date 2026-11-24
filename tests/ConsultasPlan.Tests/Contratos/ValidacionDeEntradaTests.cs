using System.Net;
using System.Net.Http.Json;
using ConsultasPlan.Contratos;
using ConsultasPlan.Controllers;
using ConsultasPlan.Models;
using ConsultasPlan.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConsultasPlan.Tests.Contratos;

/// <summary>
/// Validación de entrada y cierre del over-posting.
/// </summary>
public class ValidacionDeEntradaTests : IClassFixture<ApiDePrueba>
{
    private readonly ApiDePrueba _api;

    public ValidacionDeEntradaTests(ApiDePrueba api) => _api = api;

    // La API serializa los enums como texto. El cliente de test tiene que leer
    // con la misma convención.
    private static readonly System.Text.Json.JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private (int afiliadoId, int profesionalId) Sembrar()
    {
        using var db = _api.CrearContexto();
        db.Database.EnsureCreated();
        var afiliado = db.AgregarAfiliado(dni: $"3{Random.Shared.Next(1000000, 9999999)}");
        var profesional = db.AgregarProfesional();
        return (afiliado.Id, profesional.Id);
    }

    // Estos dos atraviesan el pipeline HTTP. Llamando al controlador directo,
    // ValidationProblem deja Status en null: lo completa ProblemDetailsFactory,
    // que se resuelve por inyección y no existe fuera del pipeline. Verificar
    // el código real exige el pipeline real.

    // El código base aceptaba agendar en 1990.
    [Fact]
    public async Task No_se_puede_agendar_una_consulta_en_el_pasado()
    {
        var (afiliadoId, profesionalId) = Sembrar();
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/consultas", new
        {
            afiliadoId,
            profesionalId,
            fechaHora = Reloj.Ahora.AddYears(-30),
            modalidad = "Presencial",
            motivo = "Consulta en el pasado"
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Tampoco_en_el_instante_exacto_actual()
    {
        var (afiliadoId, profesionalId) = Sembrar();
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/consultas", new
        {
            afiliadoId,
            profesionalId,
            fechaHora = Reloj.Ahora,
            modalidad = "Presencial",
            motivo = "Justo ahora"
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // El over-posting ya no se cierra con una comprobación: el campo no existe
    // en el contrato, así que el deserializador lo descarta.
    [Fact]
    public async Task Un_afiliado_anidado_en_el_cuerpo_se_ignora()
    {
        var (afiliadoId, profesionalId) = Sembrar();
        int afiliadosAntes;
        using (var db = _api.CrearContexto()) afiliadosAntes = db.Afiliados.Count();

        var cliente = _api.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/consultas", new
        {
            afiliadoId,
            profesionalId,
            fechaHora = Reloj.Ahora.AddDays(40),
            modalidad = "Presencial",
            motivo = "Con afiliado anidado",
            afiliado = new { nombreCompleto = "Inyectado", dni = "99999999" },
            estado = "Atendido"
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var verificacion = _api.CrearContexto();
        Assert.Equal(afiliadosAntes, verificacion.Afiliados.Count());
    }

    // El estado enviado por el cliente tampoco entra: toda consulta nace
    // Pendiente, lo decide el servidor.
    [Fact]
    public async Task El_estado_enviado_por_el_cliente_se_ignora()
    {
        var (afiliadoId, profesionalId) = Sembrar();
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/consultas", new
        {
            afiliadoId,
            profesionalId,
            fechaHora = Reloj.Ahora.AddDays(41),
            modalidad = "Presencial",
            motivo = "Intento de forzar el estado",
            estado = "Atendido"
        });

        var creada = await respuesta.Content.ReadFromJsonAsync<ConsultaResponse>(Json);
        Assert.Equal(EstadoConsulta.Pendiente, creada!.Estado);
    }

    [Theory]
    [InlineData("", "El motivo vacío")]
    [InlineData("ab", "Un motivo de dos caracteres")]
    public async Task Un_motivo_invalido_se_rechaza(string motivo, string caso)
    {
        var (afiliadoId, profesionalId) = Sembrar();
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/consultas", new
        {
            afiliadoId,
            profesionalId,
            fechaHora = Reloj.Ahora.AddDays(42),
            modalidad = "Presencial",
            motivo
        });

        Assert.True(respuesta.StatusCode == HttpStatusCode.BadRequest,
            $"{caso} debía rechazarse.");
    }

    [Theory]
    [InlineData("123", "DNI demasiado corto")]
    [InlineData("12.345.678", "DNI con puntos")]
    [InlineData("abcdefgh", "DNI no numérico")]
    public async Task Un_DNI_invalido_se_rechaza(string dni, string caso)
    {
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/afiliados", new
        {
            nombreCompleto = "Afiliado de Prueba",
            dni,
            email = "afiliado@test.local",
            telefono = "1145678901"
        });

        Assert.True(respuesta.StatusCode == HttpStatusCode.BadRequest,
            $"{caso} debía rechazarse.");
    }

    [Fact]
    public async Task Un_email_mal_formado_se_rechaza()
    {
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/afiliados", new
        {
            nombreCompleto = "Afiliado de Prueba",
            dni = "31234567",
            email = "esto-no-es-un-email",
            telefono = "1145678901"
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // El DNI identifica a la persona: dos registros con el mismo documento
    // parten el historial y el cupo entre dos afiliados que son uno solo.
    [Fact]
    public async Task No_se_pueden_crear_dos_afiliados_con_el_mismo_DNI()
    {
        var cliente = _api.CreateClient();
        var afiliado = new
        {
            nombreCompleto = "Afiliado Duplicado",
            dni = "29999999",
            email = "duplicado@test.local",
            telefono = "1145678901"
        };

        var primera = await cliente.PostAsJsonAsync("/afiliados", afiliado);
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        var segunda = await cliente.PostAsJsonAsync("/afiliados", afiliado);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    // Campos que no son parte de la ficha de contacto no se pueden tocar desde
    // la edición: bloquear o dar de baja tienen reglas propias.
    [Fact]
    public async Task La_edicion_no_puede_desbloquear_ni_cambiar_el_plan()
    {
        var cliente = _api.CreateClient();
        var creado = await cliente.PostAsJsonAsync("/afiliados", new
        {
            nombreCompleto = "Afiliado Bloqueado",
            dni = "28888888",
            email = "bloqueado@test.local",
            telefono = "1145678901"
        });
        var afiliado = await creado.Content.ReadFromJsonAsync<AfiliadoResponse>();

        using (var db = _api.CrearContexto())
        {
            var entidad = db.Afiliados.Find(afiliado!.Id)!;
            entidad.Bloqueado = true;
            db.SaveChanges();
        }

        await cliente.PutAsJsonAsync($"/afiliados/{afiliado!.Id}", new
        {
            nombreCompleto = "Nombre Editado",
            dni = "28888888",
            email = "bloqueado@test.local",
            telefono = "1145678901",
            bloqueado = false,
            plan = "Plan Premium",
            activo = false
        });

        using var verificacion = _api.CrearContexto();
        var persistido = verificacion.Afiliados.Find(afiliado.Id)!;

        Assert.Equal("Nombre Editado", persistido.NombreCompleto);
        Assert.True(persistido.Bloqueado);
        Assert.Equal("Plan Integral", persistido.Plan);
        Assert.True(persistido.Activo);
    }
}
