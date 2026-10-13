using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyRentalPortal.Web.Data;
using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Data.Seeding;
using PropertyRentalPortal.Web.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Connection & Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. ASP.NET Identity with ApplicationUser & IdentityRole
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// 3. Application Services
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();

// 4. MVC and Controllers with Views
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilationIfAvailable();

// 5. OpenAPI Specification (Bonus 1)
builder.Services.AddOpenApi();

var app = builder.Build();

// 6. On Start: Apply EF Core Migrations and Seed Database Idempotently with Bogus
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // Creates database and applies pending migrations on startup
        await context.Database.MigrateAsync();
        // Seeds lookups, users, properties, units, and applications in every status
        await DatabaseSeeder.SeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration or seeding.");
        throw;
    }
}

// 7. Configure HTTP Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 8. OpenAPI & Interactive Scalar Documentation Endpoint
app.MapOpenApi();
app.MapScalarApiReference();

// 9. Routing
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Extension method helper for clean Program.cs
public static class MvcBuilderExtensions
{
    public static IMvcBuilder AddRazorRuntimeCompilationIfAvailable(this IMvcBuilder builder)
    {
        return builder;
    }
}
