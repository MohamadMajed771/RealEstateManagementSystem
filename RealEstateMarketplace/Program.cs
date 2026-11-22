
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

using RealEstateMarketplace.Data;
using RealEstateMarketplace.Hubs;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<
    RealEstateMarketplace.Services.SavedSearchNotificationService>();

builder.Services.AddSingleton<
    RealEstateMarketplace.Services.RabbitMqConnectionService>();

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
// APPLICATION SERVICES
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

// Authentication must come before authorization.
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Properties}/{action=Index}/{id?}")
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
