
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

using RealEstateMarketplace.Data;
using RealEstateMarketplace.Hubs;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;
using RealEstateMarketplace.Services.Messaging;
using SharedContracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community licence (free for students / small organisations)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// ==========================================
// APPLICATION SERVICES
// ==========================================

builder.Services.AddScoped<SavedSearchNotificationService>();

builder.Services.AddScoped<PropertySearchService>();
builder.Services.AddScoped<UserModerationService>();
builder.Services.AddMemoryCache();

// AI search: free OpenAI-compatible endpoint (Gemini free tier / Ollama),
// falls back to the built-in rule-based parser when not configured.
builder.Services.Configure<RealEstateMarketplace.Services.AiSearch.AiSearchOptions>(
    builder.Configuration.GetSection("AiSearch"));
builder.Services.AddHttpClient<RealEstateMarketplace.Services.AiSearch.AiSearchService>();

// 10 AI searches per minute per user / IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai-search", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});
builder.Services.AddScoped<RealEstateMarketplace.Services.Identity.EmailVerificationService>();
builder.Services.AddSingleton<RealEstateMarketplace.Services.Payments.DummyPaymentGateway>();
builder.Services.AddHostedService<RealEstateMarketplace.Services.Payments.PromotionExpiryService>();

// ==========================================
// RABBITMQ (System 1 <-> System 2)
// ==========================================

// Shared connection + topology (SharedContracts)
builder.Services.AddRabbitMqMessaging(
    builder.Configuration, "RealEstateMarketplace");

// Publishes OutboxMessages (e.g. TransferRequested)
builder.Services.AddHostedService<OutboxPublisherService>();

// Receives case updates from the Transfer Service
builder.Services.AddHostedService<TransferUpdatesConsumer>();

// ==========================================
// DATABASE CONNECTION
// ==========================================

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddSingleton<NotificationPushInterceptor>();

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseSqlServer(connectionString)
        // pushes new notifications live over SignalR after each save
        .AddInterceptors(sp.GetRequiredService<NotificationPushInterceptor>()));

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

// Real SMTP when SmtpSettings are configured (user-secrets),
// otherwise emails are printed to the console (development).
if (!string.IsNullOrWhiteSpace(builder.Configuration["SmtpSettings:Host"]))
{
    builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, ConsoleEmailSender>();
}

builder.Services.AddScoped<PropertyPhotoService>();

// ==========================================
// BUILD APPLICATION
// ==========================================

var app = builder.Build();

// Migrations (development), Admin role and optional seeded admin
await DataSeeder.SeedAsync(app.Services, app.Environment.IsDevelopment());

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

app.UseRateLimiter();

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
app.MapHub<NotificationHub>("/hubs/notifications");

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
