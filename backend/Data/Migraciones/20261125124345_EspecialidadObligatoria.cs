using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsultasPlan.Data.Migraciones
{
    /// <summary>
    /// Etapa 2 de 2: exige que la conversión del catálogo haya sido completa y
    /// descarta la columna de texto.
    /// </summary>
    /// <remarks>
    /// Esta etapa está pensada para aplicarse DESPUÉS de verificar que no quedó
    /// ningún profesional sin clasificar:
    ///
    /// <code>
    /// SELECT Id, NombreCompleto, Especialidad
    ///   FROM Profesionales
    ///  WHERE EspecialidadId IS NULL;
    /// </code>
    ///
    /// Si esa consulta devuelve filas, la migración falla al volver la columna
    /// obligatoria — y falla bien: la base no puede decidir a qué especialidad
    /// del catálogo corresponde un texto que nadie previó. Eso es trabajo de
    /// alguien que conozca el negocio, no de un <c>UPDATE</c>.
    ///
    /// Separar las dos etapas es lo que hace que ese trabajo sea posible: con una
    /// sola migración no hay momento en el que mirar los datos a medio convertir.
    /// </remarks>
    public partial class EspecialidadObligatoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Profesionales_Especialidades_EspecialidadId",
                table: "Profesionales");

            migrationBuilder.DropColumn(
                name: "Especialidad",
                table: "Profesionales");

            migrationBuilder.AlterColumn<int>(
                name: "EspecialidadId",
                table: "Profesionales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Profesionales_Especialidades_EspecialidadId",
                table: "Profesionales",
                column: "EspecialidadId",
                principalTable: "Especialidades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Deshace el cierre: repone la columna de texto y vuelve la referencia
        /// opcional, es decir, devuelve la base al estado de la etapa 1.
        /// </summary>
        /// <remarks>
        /// Solo esquema, sin una sola sentencia de datos. En SQLite un cambio de
        /// columna se implementa reconstruyendo la tabla, y EF advierte que
        /// cualquier <c>Sql()</c> intercalado "will be attempted while a rebuild
        /// of table is pending": correría contra la tabla vieja y el resultado se
        /// descartaría al copiar.
        ///
        /// La reconstrucción del texto va en el <c>Down</c> de la etapa 1, que es
        /// donde se hizo la conversión y donde puede ejecutarse antes de tocar el
        /// esquema. Desandar solo esta etapa deja la columna de texto vacía, lo
        /// que es inocuo: en la etapa 1 la aplicación ya lee la referencia.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Profesionales_Especialidades_EspecialidadId",
                table: "Profesionales");

            migrationBuilder.AlterColumn<int>(
                name: "EspecialidadId",
                table: "Profesionales",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Especialidad",
                table: "Profesionales",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_Profesionales_Especialidades_EspecialidadId",
                table: "Profesionales",
                column: "EspecialidadId",
                principalTable: "Especialidades",
                principalColumn: "Id");
        }
    }
}
