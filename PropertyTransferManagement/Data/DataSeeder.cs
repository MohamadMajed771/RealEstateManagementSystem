using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Data
{
    /// <summary>
    /// Startup seeding:
    /// - Development: applies pending EF Core migrations.
    /// - Roles "Admin" and "Officer".
    /// - Default document types for the case checklist.
    /// - Staff accounts listed under "SeedUsers" in configuration
    ///   (local appsettings.Development.json / user-secrets only).
    /// </summary>
    public static class DataSeeder
    {
        public const string AdminRole = "Admin";
        public const string OfficerRole = "Officer";

        // Code, name, description, required from
        private static readonly (string Code, string Name, string Description, CaseParticipantRole Role)[] DocumentTypes =
        {
            ("BUYER_ID", "Buyer identity document",
                "National ID card or passport of the buyer (PDF or image).", CaseParticipantRole.Buyer),
            ("SELLER_ID", "Seller identity document",
                "National ID card or passport of the seller (PDF or image).", CaseParticipantRole.Seller),
            ("OWNERSHIP_DEED", "Property ownership document",
                "Title deed / property registration extract proving the seller owns the property.", CaseParticipantRole.Seller),
            ("SALE_AGREEMENT", "Sale agreement",
                "Signed sale agreement or preliminary contract between buyer and seller.", CaseParticipantRole.Buyer)
        };

        public static CaseParticipantRole? RequiredFromRole(string code) =>
            DocumentTypes.Where(d => d.Code == code).Select(d => (CaseParticipantRole?)d.Role).FirstOrDefault();

        public static async Task SeedAsync(IServiceProvider services, bool isDevelopment)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();

            if (isDevelopment)
            {
                await db.Database.MigrateAsync();
            }

            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { AdminRole, OfficerRole })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            foreach (var (code, name, description, _) in DocumentTypes)
            {
                if (!await db.DocumentTypes.AnyAsync(d => d.Code == code))
                {
                    db.DocumentTypes.Add(new DocumentType
                    {
                        Code = code,
                        Name = name,
                        Description = description,
                        IsActive = true
                    });
                }
            }
            await db.SaveChangesAsync();

            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var config = sp.GetRequiredService<IConfiguration>();

            foreach (var seed in config.GetSection("SeedUsers").GetChildren())
            {
                var email = seed["Email"];
                var password = seed["Password"];
                var role = seed["Role"];

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    continue;

                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        FirstName = seed["FirstName"] ?? "Staff",
                        LastName = seed["LastName"] ?? "Member",
                        JobTitle = role == OfficerRole ? "Case Officer" : "System Administrator"
                    };

                    var result = await userManager.CreateAsync(user, password);
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Could not seed {email}: " +
                            string.Join("; ", result.Errors.Select(e => e.Description)));
                    }
                }

                if (!string.IsNullOrWhiteSpace(role) && !await userManager.IsInRoleAsync(user, role))
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
}
