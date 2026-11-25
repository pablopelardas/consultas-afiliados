using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ConsultasPlan.Data.Migraciones
{
    /// <summary>
    /// Etapa 1 de 2: crea el catálogo y convierte los datos existentes, sin
    /// exigir todavía que la conversión haya sido completa.
    /// </summary>
    /// <remarks>
    /// Esta migración es siempre segura de aplicar: la referencia nueva admite
    /// nulos y la columna de texto se conserva, así que nada se pierde y nada
    /// falla. Lo que no haya podido convertirse queda en <c>NULL</c> y es
    /// detectable con una consulta antes de seguir.
    ///
    /// El scaffolding automático genera esto en un solo paso y descartando el
    /// texto ANTES de crear el catálogo, con lo que la información a convertir
    /// ya no existe cuando haría falta — EF lo advierte al generarla
    /// ("may result in the loss of data").
    /// </remarks>
    public partial class CatalogoDeEspecialidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. El catálogo, con sus filas: son parte del esquema porque una
            //    regla de negocio depende de que la fila NUT exista.
            migrationBuilder.CreateTable(
                name: "Especialidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Especialidades", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Especialidades",
                columns: new[] { "Id", "Codigo", "Nombre" },
                values: new object[,]
                {
                    { 1, "CLI", "Clínica Médica" },
                    { 2, "PED", "Pediatría" },
                    { 3, "CAR", "Cardiología" },
                    { 4, "NUT", "Nutrición" },
                    { 5, "TRA", "Traumatología" }
                });

            // El índice único es lo que sostiene que el código sea identidad.
            migrationBuilder.CreateIndex(
                name: "IX_Especialidades_Codigo",
                table: "Especialidades",
                column: "Codigo",
                unique: true);

            // 2. La referencia, nullable: hay que poder dejarla vacía para lo
            //    que no se pueda convertir.
            migrationBuilder.AddColumn<int>(
                name: "EspecialidadId",
                table: "Profesionales",
                type: "INTEGER",
                nullable: true);

            // 3. Conversión de los datos existentes. El texto libre es lo único
            //    con lo que se cuenta, así que se empareja por nombre exacto.
            migrationBuilder.Sql("""
                UPDATE Profesionales
                   SET EspecialidadId = (SELECT e.Id
                                           FROM Especialidades e
                                          WHERE e.Nombre = Profesionales.Especialidad);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Profesionales_EspecialidadId",
                table: "Profesionales",
                column: "EspecialidadId");

            migrationBuilder.AddForeignKey(
                name: "FK_Profesionales_Especialidades_EspecialidadId",
                table: "Profesionales",
                column: "EspecialidadId",
                principalTable: "Especialidades",
                principalColumn: "Id");
        }

        /// <summary>
        /// Deshace la conversión: reconstruye el texto desde el catálogo y
        /// descarta todo lo que esta etapa agregó.
        /// </summary>
        /// <remarks>
        /// Está escrito a mano, en SQL, por dos limitaciones de SQLite que el
        /// generador automático no resuelve bien en este caso:
        ///
        /// 1. SQLite no sabe eliminar una restricción, así que quitar la clave
        ///    foránea exige reconstruir la tabla. Con las operaciones de
        ///    <c>MigrationBuilder</c>, EF emite el <c>DROP TABLE Especialidades</c>
        ///    antes de esa reconstrucción, y la tabla todavía está referenciada:
        ///    falla con "FOREIGN KEY constraint failed". El orden correcto es
        ///    reconstruir primero y descartar el catálogo después.
        ///
        /// 2. <c>PRAGMA foreign_keys</c> es un no-op dentro de una transacción, y
        ///    sin él el <c>DROP TABLE Profesionales</c> de la reconstrucción falla
        ///    si hay consultas que lo referencian. De ahí
        ///    <c>suppressTransaction</c>: es lo que hace que el pragma sirva.
        ///
        /// El <c>UPDATE</c> que repone el texto va primero y sí dentro de la
        /// transacción, porque solo toca datos.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Profesionales
                   SET Especialidad = COALESCE((SELECT e.Nombre
                                                  FROM Especialidades e
                                                 WHERE e.Id = Profesionales.EspecialidadId), '');
                """);

            migrationBuilder.Sql("""
                PRAGMA foreign_keys = 0;

                DROP INDEX IF EXISTS "IX_Profesionales_EspecialidadId";

                CREATE TABLE "Profesionales_sin_catalogo" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Profesionales" PRIMARY KEY AUTOINCREMENT,
                    "NombreCompleto" TEXT NOT NULL,
                    "Especialidad" TEXT NOT NULL,
                    "SedeId" INTEGER NOT NULL,
                    CONSTRAINT "FK_Profesionales_Sedes_SedeId"
                        FOREIGN KEY ("SedeId") REFERENCES "Sedes" ("Id") ON DELETE CASCADE
                );

                INSERT INTO "Profesionales_sin_catalogo"
                    ("Id", "NombreCompleto", "Especialidad", "SedeId")
                SELECT "Id", "NombreCompleto", "Especialidad", "SedeId" FROM "Profesionales";

                DROP TABLE "Profesionales";
                ALTER TABLE "Profesionales_sin_catalogo" RENAME TO "Profesionales";

                DROP TABLE "Especialidades";

                PRAGMA foreign_keys = 1;
                """, suppressTransaction: true);
        }
    }
}
