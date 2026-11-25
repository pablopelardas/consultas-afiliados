namespace ConsultasPlan.Models;

public class Profesional
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;

    // Era texto libre. La política de cupo habría tenido que comparar contra el
    // literal "Nutrición", y un error de tipeo al dar de alta un profesional la
    // habría desactivado sin que nadie se enterara.
    public int EspecialidadId { get; set; }
    public Especialidad? Especialidad { get; set; }

    public int SedeId { get; set; }
    public Sede? Sede { get; set; }
}
