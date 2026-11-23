
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

using RealEstateMarketplace.Data;
using RealEstateMarketplace.Hubs;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// APPLICATION SERVICES
// ==========================================

builder.Services.AddScoped<SavedSearchNotificationService>();

builder.Services.AddSingleton<RabbitMqConnectionService>();

// ==========================================
// DATABASE CONNECTION
// ==========================================

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ==========================================
// ASP.NET CORE IDENTITY
// ==========================================

// ApplicationUser is defined in Models.
// GlobalUsings.cs provides the ApplicationUser alias.

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// ==========================================
// MVC
// ==========================================

builder.Services.AddControllersWithViews();

// ==========================================
// SIGNALR - REAL-TIME CHAT
// ==========================================

builder.Services.AddSignalR();

// ==========================================
// EMAIL AND PHOTO SERVICES
// ==========================================

builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

builder.Services.AddScoped<PropertyPhotoService>();

// ==========================================
// BUILD APPLICATION
// ==========================================

var app = builder.Build();

// ==========================================
// ERROR HANDLING
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ==========================================
// HTTP MIDDLEWARE
// ==========================================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Authentication before authorization
app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// STATIC ASSETS
// ==========================================

app.MapStaticAssets();

// ==========================================
// SIGNALR ENDPOINT
// ==========================================

app.MapHub<ChatHub>("/chatHub");

// ==========================================
// MVC ROUTING
// ==========================================

// Admin Area and other MVC Areas
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

// Public marketplace controllers
// Explicitly use the empty Area for public routes.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Properties}/{action=Index}/{id?}",
    defaults: new { area = "" })
    .WithStaticAssets();

// ==========================================
// IDENTITY RAZOR PAGES
// ==========================================

app.MapRazorPages()
   .WithStaticAssets();

// ==========================================
// START APPLICATION
// ==========================================

app.Run();
