using Microsoft.AspNetCore.Identity;
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
// HU-09: IDENTITY CONFIGURATION (NUEVO)
// ============================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Cookie settings (NUEVO)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
});

// ============================================
// SWAGGER CONFIGURATION (TU CONFIGURACIÓN EXISTENTE)
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
});

var app = builder.Build();

// ============================================
// SEED DATA (NUEVO - HU-10: Crear roles y admin inicial)
// ============================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await SeedData.Initialize(services, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error al inicializar datos de prueba");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // HABILITAR SWAGGER SOLO EN DESARROLLO
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "News Ingestor API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseRouting();

// ============================================
// HU-09: Authentication & Authorization (NUEVO)
// ============================================
app.UseAuthentication();
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