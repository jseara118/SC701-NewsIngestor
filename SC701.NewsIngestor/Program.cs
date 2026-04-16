using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;
using SC701.NewsIngestor.Services.Ingestion;

// ──────────────────────────────────────────────────────
// Aliases para evitar ambigüedad con SC701.Architecture
// ──────────────────────────────────────────────────────
using IngestReader = SC701.NewsIngestor.Services.Ingestion.ISourceReader;
using IngestService = SC701.NewsIngestor.Services.Ingestion.ISourceIngestionService;
using ArchReader = SC701.Architecture.ISourceReader;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// EF Core + SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================
// IDENTITY (HU-09)
// ============================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ============================================
// INGESTA DE FUENTES (HU-17, HU-18)
// ============================================
builder.Services.AddHttpClient();
builder.Services.AddScoped<IngestReader, JsonSourceReader>();
builder.Services.AddScoped<IngestReader, XmlSourceReader>();
builder.Services.AddScoped<IngestReader, HtmlSourceReader>();
builder.Services.AddScoped<IngestReader, NewsApiSourceReader>();
builder.Services.AddScoped<IngestService, SourceIngestionService>();

// ============================================
// SC701.Architecture (HomeController + ItemsController)
// ============================================
builder.Services.AddScoped<SC701.Architecture.INormalizationService, SC701.Architecture.Services.NormalizationService>(); // 👈 esta
builder.Services.AddScoped<ArchReader, SC701.Architecture.Services.JsonSourceReader>();
builder.Services.AddScoped<ArchReader, SC701.Architecture.Services.XmlSourceReader>();
builder.Services.AddScoped<ArchReader, SC701.Architecture.Services.HtmlSourceReader>();
builder.Services.AddScoped<SC701.Architecture.Services.SourceReaderService>();
// ============================================
// SWAGGER
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
// SEED DATA (HU-10)
// ============================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await SeedData.Initialize(scope.ServiceProvider, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error al inicializar datos de prueba");
    }
}

// ============================================
// PIPELINE
// ============================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "News Ingestor API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();

// Middleware de sesión única
app.Use(async (context, next) =>
{
    var isApiRequest = context.Request.Path.StartsWithSegments("/api");

    if (!isApiRequest && context.User?.Identity?.IsAuthenticated == true)
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

            var now = DateTime.UtcNow;
            if (user.LastActivityAt == null || (now - user.LastActivityAt.Value) > TimeSpan.FromMinutes(1))
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllers();
app.Run();