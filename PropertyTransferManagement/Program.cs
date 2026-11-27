using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;
using PropertyTransferManagement.Services.Messaging;
using SharedContracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community licence (free for students / small organisations)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// ==========================================
// DATABASE (PropertyTransferDB)
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
// IDENTITY (separate from the Marketplace)
// ==========================================

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Real SMTP when SmtpSettings are configured, otherwise
// emails (verification links) are printed to the console.
if (!string.IsNullOrWhiteSpace(builder.Configuration["SmtpSettings:Host"]))
{
    builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, ConsoleEmailSender>();
}

// ==========================================
// APPLICATION SERVICES
// ==========================================

builder.Services.AddScoped<CaseService>();
builder.Services.AddScoped<CaseChatService>();
builder.Services.AddScoped<CaseAccess>();
builder.Services.AddScoped<CaseEmailer>();
builder.Services.AddSingleton<PropertyTransferManagement.Services.Payments.DummyPaymentGateway>();
builder.Services.AddHostedService<AppointmentReminderService>();

// SignalR: case chat + live notifications
builder.Services.AddSignalR();
builder.Services.AddSingleton<DocumentStorage>();

builder.Services.AddControllersWithViews();

// Both apps run on localhost and browsers share cookies across ports,
// so each app needs its own cookie names or signing in to one signs you out of the other.
builder.Services.ConfigureApplicationCookie(o => o.Cookie.Name = "TransferService.Auth");
builder.Services.Configure<Microsoft.AspNetCore.Mvc.CookieTempDataProviderOptions>(
    o => o.Cookie.Name = "TransferService.TempData");

// Re-check the security stamp every minute, so suspending a user or
// removing a role signs them out quickly (default is 30 minutes).
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

// ==========================================
// RABBITMQ (System 1 <-> System 2)
// ==========================================

builder.Services.AddRabbitMqMessaging(
    builder.Configuration, "PropertyTransferManagement");

// Receives TransferRequested / TransferCancelled
builder.Services.AddHostedService<TransferRequestsConsumer>();

// Publishes TransferCaseCreated, TransferStatusChanged, ...
builder.Services.AddHostedService<OutboxPublisherService>();

var app = builder.Build();

// Migrations (development), roles, document types, staff accounts
await DataSeeder.SeedAsync(app.Services, app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.MapHub<PropertyTransferManagement.Hubs.CaseChatHub>("/hubs/case-chat");
app.MapHub<PropertyTransferManagement.Hubs.NotificationHub>("/hubs/notifications");

app.Run();
