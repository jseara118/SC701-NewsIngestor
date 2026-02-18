using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SC701.Architecture;
using SC701.Architecture.Services;
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

    // ✅ 20 min de inactividad
    options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    options.SlidingExpiration = true; // renueva el cookie mientras el user siga activo

    // hardening recomendado
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ============================================
// HU-18: SERVICIOS DE NORMALIZACIÓN Y LECTURA DE FUENTES
// ============================================
builder.Services.AddHttpClient(); // Para los SourceReaders (IHttpClientFactory)
builder.Services.AddScoped<INormalizationService, NormalizationService>();
builder.Services.AddScoped<ISourceReader, JsonSourceReader>();
builder.Services.AddScoped<ISourceReader, XmlSourceReader>();
builder.Services.AddScoped<ISourceReader, HtmlSourceReader>();
builder.Services.AddScoped<SourceReaderService>();

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

app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();

        var userId = userManager.GetUserId(context.User);
        var sidClaim = context.User.FindFirst("sid")?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var user = await userManager.FindByIdAsync(userId);

            if (user == null ||
                string.IsNullOrWhiteSpace(user.CurrentSessionId) ||
                string.IsNullOrWhiteSpace(sidClaim) ||
                user.CurrentSessionId != sidClaim)
            {
                await signInManager.SignOutAsync();
                context.Response.Redirect("/Account/Login?error=session_conflict");
                return;
            }

            // actualizar actividad (solo cada minuto para no saturar DB)
            var now = DateTime.UtcNow;
            if (user.LastActivityAt == null ||
                (now - user.LastActivityAt.Value) > TimeSpan.FromMinutes(1))
            {
                user.LastActivityAt = now;
                await userManager.UpdateAsync(user);
            }
        }
    }

    await next();
});


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