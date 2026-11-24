using System.ComponentModel.DataAnnotations;
using ConsultasPlan.Models;

namespace ConsultasPlan.Contratos;

/// <summary>
/// Contratos de entrada de la API.
/// </summary>
/// <remarks>
/// El código base recibía las entidades de EF directamente
/// (<c>[FromBody] Consulta</c>, <c>[FromBody] Afiliado</c>). Eso expone todas
/// sus propiedades como campos de entrada: un cliente podía enviar
/// <c>Bloqueado</c>, <c>ConsultasUsadas</c>, <c>Activo</c> o un objeto
/// <c>Afiliado</c> anidado, y EF lo persistía.
///
/// Un DTO declara exactamente qué acepta la operación. Lo que no está acá no
/// entra, y no por una comprobación que alguien tiene que acordarse de
/// escribir, sino porque no existe en el tipo.
/// </remarks>
public class CrearConsultaRequest
{
    [Required(ErrorMessage = "El afiliado es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El afiliado indicado no es válido.")]
    public int AfiliadoId { get; set; }

    [Required(ErrorMessage = "El profesional es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El profesional indicado no es válido.")]
    public int ProfesionalId { get; set; }

    [Required(ErrorMessage = "La fecha y hora son obligatorias.")]
    public DateTime FechaHora { get; set; }

    [Required(ErrorMessage = "La modalidad es obligatoria.")]
    [EnumDataType(typeof(ModalidadConsulta), ErrorMessage = "La modalidad indicada no es válida.")]
    public ModalidadConsulta Modalidad { get; set; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(300, MinimumLength = 3,
        ErrorMessage = "El motivo debe tener entre 3 y 300 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}

public class CrearAfiliadoRequest
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(120, MinimumLength = 3,
        ErrorMessage = "El nombre completo debe tener entre 3 y 120 caracteres.")]
    public string NombreCompleto { get; set; } = string.Empty;

    // Sin puntos ni separadores: el formato se normaliza en el cliente, no se
    // persisten dos representaciones del mismo documento.
    [Required(ErrorMessage = "El DNI es obligatorio.")]
    [RegularExpression(@"^\d{7,8}$",
        ErrorMessage = "El DNI debe tener 7 u 8 dígitos, sin puntos ni espacios.")]
    public string DNI { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(@"^\d{8,15}$",
        ErrorMessage = "El teléfono debe tener entre 8 y 15 dígitos.")]
    public string Telefono { get; set; } = string.Empty;

    [StringLength(60)]
    public string Plan { get; set; } = "Plan Integral";
}

/// <summary>
/// Datos de contacto editables de un afiliado.
/// </summary>
/// <remarks>
/// No incluye <c>Plan</c>, <c>Bloqueado</c>, <c>ConsultasUsadas</c> ni
/// <c>Activo</c>: cambiar el plan, bloquear o dar de baja son operaciones con
/// reglas propias, no una edición de ficha.
/// </remarks>
public class ActualizarAfiliadoRequest
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(120, MinimumLength = 3,
        ErrorMessage = "El nombre completo debe tener entre 3 y 120 caracteres.")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DNI es obligatorio.")]
    [RegularExpression(@"^\d{7,8}$",
        ErrorMessage = "El DNI debe tener 7 u 8 dígitos, sin puntos ni espacios.")]
    public string DNI { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(@"^\d{8,15}$",
        ErrorMessage = "El teléfono debe tener entre 8 y 15 dígitos.")]
    public string Telefono { get; set; } = string.Empty;
}

public class ActualizarEstadoRequest
{
    [EnumDataType(typeof(EstadoConsulta), ErrorMessage = "El estado indicado no es válido.")]
    public EstadoConsulta Estado { get; set; }
}
