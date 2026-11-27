using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RealEstateMarketplace.Data
{
    /// <summary>
    /// Startup seeding:
    /// - Development: applies pending EF Core migrations.
    /// - Always: makes sure the "Admin" role exists.
    /// - If "SeedAdmin:Email" and "SeedAdmin:Password" are configured
    ///   (local appsettings.Development.json / user-secrets), creates
    ///   that admin account with a confirmed email.
    /// </summary>
    public static class DataSeeder
    {
        public const string AdminRole = "Admin";

        public static async Task SeedAsync(IServiceProvider services, bool isDevelopment)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;

            if (isDevelopment)
            {
                await sp.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
            }

            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(AdminRole))
            {
                await roleManager.CreateAsync(new IdentityRole(AdminRole));
            }

            var config = sp.GetRequiredService<IConfiguration>();
            var email = config["SeedAdmin:Email"];
            var password = config["SeedAdmin:Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return;

            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await userManager.FindByEmailAsync(email);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = "Marketplace",
                    LastName = "Admin"
                };

                var result = await userManager.CreateAsync(admin, password);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Could not seed admin: " +
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(admin, AdminRole))
            {
                await userManager.AddToRoleAsync(admin, AdminRole);
            }
        }
    }
}
