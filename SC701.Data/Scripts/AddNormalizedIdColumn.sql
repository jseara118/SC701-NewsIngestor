-- Script para agregar la columna NormalizedId a SourceItems
-- Ejecutar este script directamente en SQL Server si la migración no funcionó

-- Verificar y agregar la columna si no existe
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SourceItems]') AND name = 'NormalizedId')
BEGIN
    ALTER TABLE [SourceItems] ADD [NormalizedId] nvarchar(500) NULL;
    PRINT 'Columna NormalizedId agregada exitosamente';
END
ELSE
BEGIN
    PRINT 'La columna NormalizedId ya existe';
END
GO

-- Verificar y crear el índice único si no existe
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SourceItems_NormalizedId' AND object_id = OBJECT_ID(N'[dbo].[SourceItems]'))
BEGIN
    CREATE UNIQUE INDEX [IX_SourceItems_NormalizedId] ON [SourceItems] ([NormalizedId]) WHERE [NormalizedId] IS NOT NULL;
    PRINT 'Índice IX_SourceItems_NormalizedId creado exitosamente';
END
ELSE
BEGIN
    PRINT 'El índice IX_SourceItems_NormalizedId ya existe';
END
GO

