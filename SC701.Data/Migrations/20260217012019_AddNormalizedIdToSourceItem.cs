using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SC701.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedIdToSourceItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Verificar si la columna ya existe antes de agregarla
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[SourceItems]') AND name = 'NormalizedId')
                BEGIN
                    ALTER TABLE [SourceItems] ADD [NormalizedId] nvarchar(500) NULL;
                END
            ");

            // Verificar si el índice ya existe antes de crearlo
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SourceItems_NormalizedId' AND object_id = OBJECT_ID(N'[SourceItems]'))
                BEGIN
                    CREATE UNIQUE INDEX [IX_SourceItems_NormalizedId] ON [SourceItems] ([NormalizedId]) WHERE [NormalizedId] IS NOT NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eliminar índice solo si existe
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SourceItems_NormalizedId' AND object_id = OBJECT_ID(N'[SourceItems]'))
                BEGIN
                    DROP INDEX [IX_SourceItems_NormalizedId] ON [SourceItems];
                END
            ");

            // Eliminar columna solo si existe
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[SourceItems]') AND name = 'NormalizedId')
                BEGIN
                    ALTER TABLE [SourceItems] DROP COLUMN [NormalizedId];
                END
            ");
        }
    }
}
