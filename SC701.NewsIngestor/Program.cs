using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// EF Core + SQL Server (LocalDB)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================
// SWAGGER CONFIGURATION
// ============================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SC701 News Ingestor API",
        Version = "v1",
        Description = "API para gestionar fuentes de noticias y sus items",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Equipo SC701",
            Email = "contact@newsingestor.com"
        }
    });

    // Habilitar comentarios XML si los tienes
    // var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    // var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    // options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // ============================================
    // HABILITAR SWAGGER SOLO EN DESARROLLO
    // ============================================
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "News Ingestor API v1");
        options.RoutePrefix = "swagger"; // URL: https://localhost:xxxx/swagger
    });
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// Mapeo de rutas para MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Mapeo de rutas para API Controllers
app.MapControllers();

app.Run();