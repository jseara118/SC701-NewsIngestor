# SC701-NewsIngestor

Aplicación web ASP.NET Core MVC para ingestión y normalización de noticias desde múltiples fuentes externas (RSS, NewsAPI, Web Scraper, JSON).

---

## Requisitos previos

| Herramienta | Versión mínima |
|---|---|
| Visual Studio | 2022 (v17.8+) |
| .NET SDK | 9.0 |
| SQL Server | LocalDB o SQL Server Express/Full |

---

## Configuración de la cadena de conexión

### Opción A — SQL Server LocalDB (recomendado, ya incluido en Visual Studio)

En `SC701.NewsIngestor/appsettings.json`, la cadena ya está lista:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=NewsIngestorDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

No requiere instalación adicional si ya tiene Visual Studio 2022.

---

### Opción B — SQL Server Express o instancia local

Si prefiere usar una instancia de SQL Server propia, siga estos pasos para obtener el string de conexión desde Visual Studio:

1. Abrir **View → SQL Server Object Explorer** (`Ctrl+\`, `Ctrl+S`)
2. Expandir **SQL Server → (localdb)\MSSQLLocalDB** (o su instancia)
3. Clic derecho sobre el servidor → **Properties**
4. Copiar el valor del campo **Connection String**

Reemplazar el valor de `DefaultConnection` en `appsettings.json` con el string copiado. Ejemplo para SQL Server Express:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=NewsIngestorDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

---

## Pasos para correr el proyecto

### 1. Clonar o descomprimir el proyecto

Abrir la solución `SC701-NewsIngestor.sln` en Visual Studio 2022.

### 2. Restaurar paquetes NuGet

Visual Studio lo hace automáticamente al abrir la solución. Si no:

```
Tools → NuGet Package Manager → Manage NuGet Packages for Solution → Restore
```

### 3. Aplicar migraciones y crear la base de datos

Abrir **Package Manager Console** (`Tools → NuGet Package Manager → Package Manager Console`) y asegurarse de que el **Default project** sea `SC701.Data`:

```powershell
Update-Database
```

Esto crea la base de datos y todas las tablas automáticamente.

### 4. Cargar datos de prueba (opcional)

Si desea cargar los datos existentes de la demo, ejecutar el script SQL incluido:

1. Abrir **SQL Server Object Explorer**
2. Conectarse a la base `NewsIngestorDB`
3. Clic derecho → **New Query**
4. Abrir y ejecutar el archivo `Scripts/seed_data.sql` incluido en el proyecto

### 5. Correr la aplicación

Presionar **F5** o el botón ▶ en Visual Studio. La aplicación abre en `https://localhost:{puerto}`.

---

## Credenciales de acceso

| Rol | Email | Contraseña |
|---|---|---|
| Admin | admin@newsingestor.com | Admin123! |

> El usuario Admin se crea automáticamente al iniciar la aplicación por primera vez mediante el `SeedData`.

---

## Funcionalidades principales

- **Fuentes** — CRUD completo de fuentes de noticias (RSS, NewsAPI, Web Scraper, JSON Feed)
- **Web Scraper** — Soporta múltiples URLs por fuente en el campo "URLs Adicionales"
- **Ítems** — Visualización, importación (JSON) y eliminación de noticias
- **Preview en vivo** — Vista previa de noticias desde la fuente sin guardarlas
- **Importar JSON** — Carga de noticias en formato `edu.univ.ingest.v1`
- **Exportar JSON** — Descarga individual de cualquier noticia en formato estándar
- **Swagger** — Documentación de API disponible en `/swagger` (solo en desarrollo)
- **Autenticación** — Login con sesión única por usuario (ASP.NET Identity)
- **Roles** — Admin puede crear/editar/eliminar fuentes e ítems

---

## Estructura del proyecto

```
SC701-NewsIngestor/
├── SC701.NewsIngestor/        ← Proyecto principal MVC
│   ├── Controllers/           ← MVC + API controllers
│   ├── Services/Ingestion/    ← Readers por tipo de fuente
│   └── Views/                 ← Vistas Razor
├── SC701.Architecture/        ← Servicios de lectura y normalización
├── SC701.Data/                ← DbContext + Migraciones
│   └── Migrations/
└── SC701.Models/              ← Modelos y DTOs
    └── DTOs/StandardNewsItemDto.cs   ← Esquema edu.univ.ingest.v1
```

---

## Formato JSON estándar (edu.univ.ingest.v1)

Las noticias se almacenan y exportan en el formato acordado por el equipo:

```json
{
  "schemaVersion": "edu.univ.ingest.v1",
  "exportedAt": "2026-04-16T10:00:00Z",
  "source": {
    "id": "5",
    "name": "Teletica Noticias",
    "type": "api",
    "url": "https://www.teletica.com",
    "requiresSecret": false
  },
  "normalized": {
    "title": "Título de la noticia",
    "summary": "Resumen breve.",
    "content": "Contenido completo...",
    "publishedAt": "2026-04-15T08:00:00Z",
    "author": "Autor",
    "language": "es",
    "category": { "primary": "NACIONAL" }
  },
  "raw": { "format": "json", "data": {} }
}
```

---

## Notas adicionales

- **NewsAPI** requiere un API key registrado en [newsapi.org](https://newsapi.org). Configurarlo en `Configuración → Secrets` con nombre `apiKey` asociado a la fuente.
- **Web Scraper** funciona mejor con sitios que tengan meta tags Open Graph (`og:title`, `og:description`). Sitios con renderizado JavaScript pueden no devolver contenido.
- El plan gratuito de NewsAPI solo funciona desde `localhost`. Para demostración usar el endpoint `top-headlines?country=us`.
