using ConsultasPlan.Dominio.Cupo;
using ConsultasPlan.Models;

namespace ConsultasPlan.Tests.Cupo;

/// <summary>
/// Las dos interpretaciones de "año" y "mes" del enunciado.
/// </summary>
/// <remarks>
/// El enunciado no define ninguna de las dos. <see cref="PeriodoCalendario"/>
/// es la adoptada; <see cref="PeriodoVigencia"/> está implementada y testeada
/// para que la ambigüedad sea un hecho del código y no solo una nota del
/// documento técnico.
/// </remarks>
public class PeriodosDelCupoTests
{
    private static DateTime Utc(int ano, int mes, int dia, int hora = 10) =>
        new(ano, mes, dia, hora, 0, 0, DateTimeKind.Utc);

    private static Afiliado ConAltaEn(DateTime alta) =>
        new() { NombreCompleto = "Afiliado", DNI = "30000000", FechaAlta = alta, Activo = true };

    public class Calendario
    {
        private static PeriodosDelCupo Para(DateTime fecha) =>
            new PeriodoCalendario().Para(fecha, ConAltaEn(Utc(2024, 3, 14)));

        [Fact]
        public void El_ano_va_del_primero_de_enero_al_primero_del_siguiente()
        {
            var periodos = Para(Utc(2026, 6, 15));

            Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.Desde);
            Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.Hasta);
            Assert.Equal(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.UltimoDia);
        }

        [Fact]
        public void El_mes_va_del_primero_al_primero_del_siguiente()
        {
            var periodos = Para(Utc(2026, 6, 15));

            Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Desde);
            Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Hasta);
        }

        // Febrero tiene 28 o 29 días. El período se define por el primero del
        // mes siguiente, así que la cantidad de días no entra en el cálculo.
        [Theory]
        [InlineData(2026, 28)]
        [InlineData(2028, 29)]
        public void Febrero_cierra_el_primero_de_marzo_sin_importar_los_dias(int ano, int diasEsperados)
        {
            var periodos = Para(Utc(ano, 2, 10));

            Assert.Equal(new DateTime(ano, 3, 1, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Hasta);
            Assert.Equal(diasEsperados, (periodos.Mensual.Hasta - periodos.Mensual.Desde).Days);
        }

        [Fact]
        public void Diciembre_cierra_el_primero_de_enero_del_ano_siguiente()
        {
            var periodos = Para(Utc(2026, 12, 20));

            Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Hasta);
            Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.Hasta);
        }

        // No depende del afiliado: el corte es el mismo para todos.
        [Fact]
        public void No_depende_de_la_fecha_de_alta()
        {
            var proveedor = new PeriodoCalendario();
            var fecha = Utc(2026, 6, 15);

            var deUno = proveedor.Para(fecha, ConAltaEn(Utc(2020, 1, 1)));
            var deOtro = proveedor.Para(fecha, ConAltaEn(Utc(2025, 9, 30)));

            Assert.Equal(deUno, deOtro);
        }
    }

    public class Vigencia
    {
        private static PeriodosDelCupo Para(DateTime fecha, DateTime alta) =>
            new PeriodoVigencia().Para(fecha, ConAltaEn(alta));

        [Fact]
        public void El_ano_arranca_en_el_aniversario_del_alta()
        {
            var periodos = Para(Utc(2026, 6, 15), alta: Utc(2024, 3, 14));

            Assert.Equal(new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.Desde);
            Assert.Equal(new DateTime(2027, 3, 14, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.Hasta);
        }

        // Antes del aniversario de este año, el período vigente es el que
        // arrancó el año pasado.
        [Fact]
        public void Antes_del_aniversario_rige_el_periodo_anterior()
        {
            var periodos = Para(Utc(2026, 2, 1), alta: Utc(2024, 3, 14));

            Assert.Equal(new DateTime(2025, 3, 14, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.Desde);
            Assert.Equal(new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.Hasta);
        }

        [Fact]
        public void El_mes_arranca_en_el_dia_del_alta()
        {
            var periodos = Para(Utc(2026, 6, 20), alta: Utc(2024, 3, 14));

            Assert.Equal(new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Desde);
            Assert.Equal(new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Hasta);
        }

        [Fact]
        public void Antes_del_dia_del_alta_rige_el_mes_anterior()
        {
            var periodos = Para(Utc(2026, 6, 5), alta: Utc(2024, 3, 14));

            Assert.Equal(new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Desde);
            Assert.Equal(new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Hasta);
        }

        // Un alta el 31 no tiene día 31 en febrero. Sin recorte, construir la
        // fecha lanza y el cupo del afiliado deja de poder calcularse once veces
        // al año.
        [Fact]
        public void Un_alta_el_31_se_recorta_al_ultimo_dia_del_mes_corto()
        {
            // El 28 de febrero es el inicio recortado del período de febrero, y
            // cierra en el ancla de marzo —que sí tiene día 31—, no 28 días
            // después.
            var enElRecorte = Para(Utc(2026, 2, 28), alta: Utc(2024, 1, 31));

            Assert.Equal(new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc), enElRecorte.Mensual.Desde);
            Assert.Equal(new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), enElRecorte.Mensual.Hasta);
        }

        // Y antes de ese 28 todavía corre el período que arrancó el 31 de enero:
        // los períodos consecutivos no dejan hueco ni se solapan, que es la
        // propiedad que importa.
        [Fact]
        public void Antes_del_recorte_sigue_corriendo_el_periodo_del_mes_largo()
        {
            var periodos = Para(Utc(2026, 2, 20), alta: Utc(2024, 1, 31));

            Assert.Equal(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Desde);
            Assert.Equal(new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc), periodos.Mensual.Hasta);
        }

        // La propiedad que importa, y que ningún test de ejemplo cubría: todo
        // día cae en exactamente un período. Encontró un hueco de tres días en
        // marzo para las altas del 31, porque el fin se calculaba con
        // AddMonths(1) en lugar del ancla siguiente.
        [Theory]
        [InlineData(31)]
        [InlineData(30)]
        [InlineData(29)]
        [InlineData(28)]
        [InlineData(15)]
        [InlineData(1)]
        public void Todo_dia_cae_en_exactamente_un_periodo_mensual(int diaDeAlta)
        {
            var afiliado = ConAltaEn(new DateTime(2024, 1, diaDeAlta, 10, 0, 0, DateTimeKind.Utc));
            var proveedor = new PeriodoVigencia();
            var arranque = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var anterior = proveedor.Para(arranque, afiliado).Mensual;
            Assert.True(anterior.Contiene(arranque), $"{arranque:dd/MM/yyyy} no cae en su período.");

            // Tres años día por día: el período devuelto tiene que contener la
            // fecha, y cuando cambia, el nuevo arranca donde terminó el viejo.
            for (var i = 1; i <= 365 * 3; i++)
            {
                var dia = arranque.AddDays(i);
                var actual = proveedor.Para(dia, afiliado).Mensual;

                Assert.True(actual.Contiene(dia), $"{dia:dd/MM/yyyy} no cae en su período.");

                if (actual == anterior) continue;
                Assert.Equal(anterior.Hasta, actual.Desde);
                anterior = actual;
            }
        }

        // Mismo riesgo en el período anual: el ancla de un alta el 29 de febrero
        // se corre un día en los años bisiestos.
        [Theory]
        [InlineData(2, 29)]
        [InlineData(3, 14)]
        [InlineData(1, 1)]
        [InlineData(12, 31)]
        public void Todo_dia_cae_en_exactamente_un_periodo_anual(int mesDeAlta, int diaDeAlta)
        {
            var afiliado = ConAltaEn(new DateTime(2024, mesDeAlta, diaDeAlta, 10, 0, 0, DateTimeKind.Utc));
            var proveedor = new PeriodoVigencia();
            var arranque = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var anterior = proveedor.Para(arranque, afiliado).Anual;

            for (var i = 1; i <= 365 * 6; i++)
            {
                var dia = arranque.AddDays(i);
                var actual = proveedor.Para(dia, afiliado).Anual;

                Assert.True(actual.Contiene(dia), $"{dia:dd/MM/yyyy} no cae en su período anual.");

                if (actual == anterior) continue;
                Assert.Equal(anterior.Hasta, actual.Desde);
                anterior = actual;
            }
        }

        [Fact]
        public void Un_alta_el_29_de_febrero_no_rompe_en_anos_no_bisiestos()
        {
            var periodos = Para(Utc(2026, 6, 15), alta: new DateTime(2024, 2, 29, 10, 0, 0, DateTimeKind.Utc));

            Assert.Equal(new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.Desde);
            Assert.Equal(new DateTime(2027, 2, 28, 0, 0, 0, DateTimeKind.Utc), periodos.Anual.Hasta);
        }

        // La diferencia con el calendario es el punto de todo el ejercicio: la
        // misma consulta cae en períodos distintos según la interpretación.
        [Fact]
        public void La_misma_fecha_cae_en_periodos_distintos_segun_la_interpretacion()
        {
            var fecha = Utc(2026, 1, 20);
            var afiliado = ConAltaEn(Utc(2024, 3, 14));

            var calendario = new PeriodoCalendario().Para(fecha, afiliado);
            var vigencia = new PeriodoVigencia().Para(fecha, afiliado);

            Assert.Equal(2026, calendario.Anual.Desde.Year);
            Assert.Equal(2025, vigencia.Anual.Desde.Year);
        }
    }
}
