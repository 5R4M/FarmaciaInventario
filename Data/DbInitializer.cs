using FarmaciaInventario.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FarmaciaInventario.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            context.Database.EnsureCreated();

            // Add new columns to Sales if they don't exist (EnsureCreated won't update existing schema)
            try
            {
                context.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='CustomerNit')
                        ALTER TABLE Sales ADD CustomerNit NVARCHAR(20) NOT NULL DEFAULT 'CF';
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='CustomerAddress')
                        ALTER TABLE Sales ADD CustomerAddress NVARCHAR(200) NOT NULL DEFAULT '';
                ");
            }
            catch { }

            // Seed Roles
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Seed Admin User
            var adminEmail = "admin@farmacia.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                await userManager.CreateAsync(adminUser, "Admin123*");
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // Seed Regular User
            var userEmail = "vendedor@farmacia.com";
            if (await userManager.FindByEmailAsync(userEmail) == null)
            {
                var regularUser = new IdentityUser { UserName = userEmail, Email = userEmail, EmailConfirmed = true };
                await userManager.CreateAsync(regularUser, "User123*");
                await userManager.AddToRoleAsync(regularUser, "User");
            }

            if (context.Categories.Any()) return;

            // ... previous seeding code for categories and products ...
            var categories = new Category[]
            {
                new Category { Name = "Analgésicos", Description = "Medicamentos para aliviar el dolor" },
                new Category { Name = "Antialérgicos", Description = "Medicamentos para tratar alergias" },
                new Category { Name = "Antisépticos", Description = "Sustancias que inhiben el crecimiento de microorganismos" },
                new Category { Name = "Material Médico Quirúrgico", Description = "Sondas, catéteres, equipo de venoclisis, etc." },
                new Category { Name = "Bebidas", Description = "Agua, jugos, refrescos" },
                new Category { Name = "Helados y Golosinas", Description = "Postres y dulces" }
            };
            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();

            var products = new Product[]
            {
                new Product { Name = "Paracetamol 500mg", SKU = "ANA001", Price = 5.50m, StockQuantity = 100, CategoryId = categories[0].Id, ExpirationDate = DateTime.Now.AddYears(1) },
                new Product { Name = "Loratadina 10mg", SKU = "ANT001", Price = 12.00m, StockQuantity = 50, CategoryId = categories[1].Id, ExpirationDate = DateTime.Now.AddYears(2) },
                new Product { Name = "Sonda Foley #18", SKU = "MMQ001", Price = 45.00m, StockQuantity = 20, CategoryId = categories[3].Id },
                new Product { Name = "Agua Mineral 500ml", SKU = "BEB001", Price = 1.50m, StockQuantity = 200, CategoryId = categories[4].Id }
            };
            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }
    }
}
