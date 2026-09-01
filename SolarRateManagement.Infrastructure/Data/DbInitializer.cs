using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SolarRateManagement.Domain.Entities;

namespace SolarRateManagement.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static void Seed(AppDbContext context)
        {
            context.Database.EnsureCreated();

            // 1. Seed Permissions
            var permissions = new[]
            {
                new Permission { Code = "RATE_VIEW", Description = "View daily rates" },
                new Permission { Code = "RATE_CREATE", Description = "Create daily rates" },
                new Permission { Code = "RATE_EDIT", Description = "Edit daily rates" },
                new Permission { Code = "RATE_DELETE", Description = "Delete daily rates" },
                new Permission { Code = "SHOP_MANAGE", Description = "Manage shops" },
                new Permission { Code = "ITEM_MANAGE", Description = "Manage solar items" },
                new Permission { Code = "USER_MANAGE", Description = "Manage users and roles" },
                new Permission { Code = "AUDIT_VIEW", Description = "View audit logs" }
            };

            foreach (var p in permissions)
            {
                if (!context.Permissions.Any(x => x.Code == p.Code))
                {
                    context.Permissions.Add(p);
                }
            }
            context.SaveChanges();

            // Reload permissions to get generated IDs
            var dbPermissions = context.Permissions.ToList();

            // 2. Seed Roles
            var superAdminRole = context.Roles.FirstOrDefault(r => r.Name == "SuperAdmin");
            if (superAdminRole == null)
            {
                superAdminRole = new Role { Name = "SuperAdmin", Description = "System Super Administrator" };
                context.Roles.Add(superAdminRole);
            }

            var shopAdminRole = context.Roles.FirstOrDefault(r => r.Name == "ShopAdmin");
            if (shopAdminRole == null)
            {
                shopAdminRole = new Role { Name = "ShopAdmin", Description = "Shop Administrator" };
                context.Roles.Add(shopAdminRole);
            }

            var shopUserRole = context.Roles.FirstOrDefault(r => r.Name == "ShopUser");
            if (shopUserRole == null)
            {
                shopUserRole = new Role { Name = "ShopUser", Description = "Shop Standard User" };
                context.Roles.Add(shopUserRole);
            }
            context.SaveChanges();

            // 3. Seed RolePermissions
            // SuperAdmin has all permissions
            foreach (var p in dbPermissions)
            {
                if (!context.RolePermissions.Any(rp => rp.RoleId == superAdminRole.Id && rp.PermissionId == p.Id))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = superAdminRole.Id, PermissionId = p.Id });
                }
            }

            // ShopAdmin has rate management, items, users
            var shopAdminPerms = dbPermissions.Where(p => 
                p.Code == "RATE_VIEW" || p.Code == "RATE_CREATE" || p.Code == "RATE_EDIT" || 
                p.Code == "RATE_DELETE" || p.Code == "ITEM_MANAGE" || p.Code == "USER_MANAGE").ToList();

            foreach (var p in shopAdminPerms)
            {
                if (!context.RolePermissions.Any(rp => rp.RoleId == shopAdminRole.Id && rp.PermissionId == p.Id))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = shopAdminRole.Id, PermissionId = p.Id });
                }
            }

            // ShopUser has rate view, create, edit
            var shopUserPerms = dbPermissions.Where(p => 
                p.Code == "RATE_VIEW" || p.Code == "RATE_CREATE" || p.Code == "RATE_EDIT").ToList();

            foreach (var p in shopUserPerms)
            {
                if (!context.RolePermissions.Any(rp => rp.RoleId == shopUserRole.Id && rp.PermissionId == p.Id))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = shopUserRole.Id, PermissionId = p.Id });
                }
            }
            context.SaveChanges();

            // 4. Seed default SuperAdmin User
            if (!context.Users.Any(u => u.Username == "superadmin"))
            {
                var adminUser = new User
                {
                    FirstName = "System",
                    LastName = "Administrator",
                    Username = "superadmin",
                    Email = "admin@solarmanagement.com",
                    Mobile = "1234567890",
                    PasswordHash = HashPassword("Password123!"),
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                context.Users.Add(adminUser);
                context.SaveChanges();

                // Assign SuperAdmin role to user
                context.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = superAdminRole.Id });
                context.SaveChanges();
            }

            // 4.5. Seed default Shops and UserShops associations
            var shops = new[]
            {
                new Shop { Name = "Islamabad Solar HQ", OwnerName = "Ali Rahman", ContactNumber = "+9251111222", Email = "hq@islamabadsolar.com", City = "Islamabad", Address = "Sector G-11, Islamabad", IsActive = true, CreatedDate = DateTime.UtcNow },
                new Shop { Name = "Lahore Energy Hub", OwnerName = "Zubair Khan", ContactNumber = "+9242111222", Email = "hub@lahoreenergy.com", City = "Lahore", Address = "Gulberg III, Lahore", IsActive = true, CreatedDate = DateTime.UtcNow },
                new Shop { Name = "Karachi Power Center", OwnerName = "Siddiqui Bros", ContactNumber = "+9221111222", Email = "center@karachipower.com", City = "Karachi", Address = "Saddar, Karachi", IsActive = true, CreatedDate = DateTime.UtcNow },
                new Shop { Name = "Faisalabad Solar Center", OwnerName = "Tariq Mahmood", ContactNumber = "+9241111222", Email = "contact@faisalabadsolar.com", City = "Faisalabad", Address = "Clock Tower Plaza, Faisalabad", IsActive = true, CreatedDate = DateTime.UtcNow },
                new Shop { Name = "Multan Ray Energy", OwnerName = "Usman Malik", ContactNumber = "+9261111222", Email = "info@multanrayenergy.com", City = "Multan", Address = "Abdali Road, Multan", IsActive = true, CreatedDate = DateTime.UtcNow }
            };

            foreach (var s in shops)
            {
                if (!context.Shops.Any(x => x.Name == s.Name))
                {
                    context.Shops.Add(s);
                }
            }
            context.SaveChanges();

            var dbShops = context.Shops.ToList();
            var isbShop = dbShops.First(s => s.Name == "Islamabad Solar HQ");
            var lhrShop = dbShops.First(s => s.Name == "Lahore Energy Hub");

            var dbAdminUser = context.Users.First(u => u.Username == "superadmin");
            foreach (var shop in dbShops)
            {
                if (!context.UserShops.Any(us => us.UserId == dbAdminUser.Id && us.ShopId == shop.Id))
                {
                    context.UserShops.Add(new UserShop { UserId = dbAdminUser.Id, ShopId = shop.Id, RoleInShop = "SuperAdmin" });
                }
            }
            context.SaveChanges();

            // Seed a ShopManager user for Islamabad
            if (!context.Users.Any(u => u.Username == "isb_manager"))
            {
                var manager = new User
                {
                    FirstName = "Islamabad",
                    LastName = "Manager",
                    Username = "isb_manager",
                    Email = "isb_manager@solarmanagement.com",
                    Mobile = "1122334455",
                    PasswordHash = HashPassword("Password123!"),
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                context.Users.Add(manager);
                context.SaveChanges();

                context.UserRoles.Add(new UserRole { UserId = manager.Id, RoleId = shopAdminRole.Id });
                context.UserShops.Add(new UserShop { UserId = manager.Id, ShopId = isbShop.Id, RoleInShop = "ShopManager" });
                context.SaveChanges();
            }

            // Seed a ShopOperator user for Lahore
            if (!context.Users.Any(u => u.Username == "lhr_operator"))
            {
                var operatorUser = new User
                {
                    FirstName = "Lahore",
                    LastName = "Operator",
                    Username = "lhr_operator",
                    Email = "lhr_operator@solarmanagement.com",
                    Mobile = "5544332211",
                    PasswordHash = HashPassword("Password123!"),
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                context.Users.Add(operatorUser);
                context.SaveChanges();

                context.UserRoles.Add(new UserRole { UserId = operatorUser.Id, RoleId = shopUserRole.Id });
                context.UserShops.Add(new UserShop { UserId = operatorUser.Id, ShopId = lhrShop.Id, RoleInShop = "ShopOperator" });
                context.SaveChanges();
            }

            // 5. Seed default Categories (Template)
            var categories = new[]
            {
                new Category { Name = "Solar Panels", Description = "Photovoltaic Solar Panels", IsActive = true, DisplayOrder = 1 },
                new Category { Name = "Inverters", Description = "Solar Power Inverters", IsActive = true, DisplayOrder = 2 },
                new Category { Name = "Batteries", Description = "Solar Energy Storage Batteries", IsActive = true, DisplayOrder = 3 },
                new Category { Name = "Mounting Structures", Description = "Frames and structures", IsActive = true, DisplayOrder = 4 },
                new Category { Name = "Accessories", Description = "Cables, connectors, protection devices", IsActive = true, DisplayOrder = 5 }
            };

            foreach (var cat in categories)
            {
                if (!context.Categories.Any(c => c.Name == cat.Name))
                {
                    context.Categories.Add(cat);
                }
            }
            context.SaveChanges();

            // Seed default Global Template Items (ShopId = null)
            var solarPanelsCat = context.Categories.First(c => c.Name == "Solar Panels");
            var invertersCat = context.Categories.First(c => c.Name == "Inverters");

            var templateItems = new[]
            {
                new Item
                {
                    CategoryId = solarPanelsCat.Id,
                    ItemCode = "PAN-LONGI-550W",
                    ItemName = "Longi 550W Hi-MO 5",
                    Brand = "Longi",
                    Model = "Hi-MO 5 550W",
                    Unit = "W",
                    Description = "Mono-crystalline solar panel",
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedDate = DateTime.UtcNow
                },
                new Item
                {
                    CategoryId = solarPanelsCat.Id,
                    ItemCode = "PAN-JINKO-545W",
                    ItemName = "Jinko Tiger Pro 545W",
                    Brand = "Jinko",
                    Model = "Tiger Pro 545W",
                    Unit = "W",
                    Description = "Mono-crystalline solar panel",
                    IsActive = true,
                    DisplayOrder = 2,
                    CreatedDate = DateTime.UtcNow
                },
                new Item
                {
                    CategoryId = invertersCat.Id,
                    ItemCode = "INV-GROWATT-10K",
                    ItemName = "Growatt 10kW Three-Phase Hybrid",
                    Brand = "Growatt",
                    Model = "MOD 10KTL3-XH",
                    Unit = "Unit",
                    Description = "On-Grid Hybrid Inverter",
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedDate = DateTime.UtcNow
                },
                new Item
                {
                    CategoryId = invertersCat.Id,
                    ItemCode = "INV-Solis-20K",
                    ItemName = "Solis 20kW On-Grid",
                    Brand = "Solis",
                    Model = "S5-GR3P20K",
                    Unit = "Unit",
                    Description = "Three-Phase Grid-Tied Inverter",
                    IsActive = true,
                    DisplayOrder = 2,
                    CreatedDate = DateTime.UtcNow
                }
            };

            foreach (var item in templateItems)
            {
                if (!context.Items.Any(i => i.ItemCode == item.ItemCode && i.ShopId == null))
                {
                    context.Items.Add(item);
                }
            }
            context.SaveChanges();
        }

        public static string HashPassword(string password)
        {
            using (var hmac = new HMACSHA512())
            {
                byte[] salt = hmac.Key;
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
            }
        }
    }
}
