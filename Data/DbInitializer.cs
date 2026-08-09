using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        try
        {
            // Auto update database schema for PostgreSQL / SQL Server
            try
            {
                await context.Database.MigrateAsync();
            }
            catch
            {
                await context.Database.EnsureCreatedAsync();
            }
        }
        catch
        {
            await context.Database.EnsureCreatedAsync();
        }

        // Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            context.Roles.AddRange(
                new Role { RoleId = 1, RoleName = "Admin" },
                new Role { RoleId = 2, RoleName = "Customer" }
            );
            await context.SaveChangesAsync();
        }

        // Seed Admin User
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
            int adminRoleId = adminRole?.RoleId ?? 1;

            var adminUser = new User
            {
                UserGuid = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@fashionstore.vn",
                FullName = "Quản Trị Viên Hệ Thống",
                Phone = "0909999999",
                PasswordHash = HashPassword("Admin@123"),
                RoleId = adminRoleId,
                CreatedAt = DateTime.Now
            };

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            context.Carts.Add(new Cart { UserId = adminUser.UserId });
            await context.SaveChangesAsync();
        }

        // Seed Categories
        if (!await context.Categories.AnyAsync())
        {
            var cAo = new Category { CategoryName = "Áo Nam & Nữ" };
            var cQuan = new Category { CategoryName = "Quần Thời Trang" };
            var cVay = new Category { CategoryName = "Váy & Đầm" };
            var cPhuKien = new Category { CategoryName = "Phụ Kiện" };

            context.Categories.AddRange(cAo, cQuan, cVay, cPhuKien);
            await context.SaveChangesAsync();

            context.Categories.AddRange(
                new Category { CategoryName = "Áo Phông / T-Shirt", ParentCategoryId = cAo.CategoryId },
                new Category { CategoryName = "Áo Sơ Mi", ParentCategoryId = cAo.CategoryId },
                new Category { CategoryName = "Quần Jean", ParentCategoryId = cQuan.CategoryId },
                new Category { CategoryName = "Quần Short", ParentCategoryId = cQuan.CategoryId }
            );
            await context.SaveChangesAsync();
        }

        // Seed Brands
        if (!await context.Brands.AnyAsync())
        {
            context.Brands.AddRange(
                new Brand { BrandName = "Nike", LogoUrl = "https://img.freepik.com/free-icon/nike_318-566080.jpg" },
                new Brand { BrandName = "Adidas", LogoUrl = "https://img.freepik.com/free-icon/adidas_318-566070.jpg" },
                new Brand { BrandName = "Zara", LogoUrl = "https://img.freepik.com/free-icon/zara_318-566090.jpg" },
                new Brand { BrandName = "Uniqlo", LogoUrl = "https://img.freepik.com/free-icon/uniqlo_318-566100.jpg" }
            );
            await context.SaveChangesAsync();
        }

        // Seed Promotions
        if (!await context.Promotions.AnyAsync())
        {
            context.Promotions.AddRange(
                new Promotion
                {
                    Code = "FASHION2026",
                    DiscountType = DiscountType.Percentage,
                    DiscountValue = 10,
                    MinOrderValue = 300000,
                    StartDate = DateTime.Now.AddDays(-5),
                    EndDate = DateTime.Now.AddDays(60)
                },
                new Promotion
                {
                    Code = "GIAM50K",
                    DiscountType = DiscountType.FixedAmount,
                    DiscountValue = 50000,
                    MinOrderValue = 500000,
                    StartDate = DateTime.Now.AddDays(-5),
                    EndDate = DateTime.Now.AddDays(60)
                }
            );
            await context.SaveChangesAsync();
        }

        // Seed Rich Sample Products safely
        var brandNike = await context.Brands.FirstOrDefaultAsync(b => b.BrandName == "Nike") ?? await context.Brands.FirstAsync();
        var brandAdidas = await context.Brands.FirstOrDefaultAsync(b => b.BrandName == "Adidas") ?? await context.Brands.FirstAsync();
        var brandZara = await context.Brands.FirstOrDefaultAsync(b => b.BrandName == "Zara") ?? await context.Brands.FirstAsync();
        var brandUniqlo = await context.Brands.FirstOrDefaultAsync(b => b.BrandName == "Uniqlo") ?? await context.Brands.FirstAsync();

        var targetCatAo = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Áo Nam & Nữ") ?? await context.Categories.FirstAsync();
        var targetCatQuan = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Quần Thời Trang") ?? await context.Categories.FirstAsync();
        var targetCatVay = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Váy & Đầm") ?? await context.Categories.FirstAsync();
        var targetCatPhuKien = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Phụ Kiện") ?? await context.Categories.FirstAsync();

        var productsToSeed = new List<(Product product, string imgUrl, List<(string size, string color, decimal price, int stock)> variants)>
        {
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "ao-so-mi-nam-oxford-trang-premium", ProductName = "Áo Sơ Mi Nam Oxford Trắng Premium", Description = "Áo sơ mi nam chất liệu vải Cotton Oxford cao cấp, chống nhăn, phom dáng Slim-fit tôn dáng.", CategoryId = targetCatAo.CategoryId, BrandId = brandZara.BrandId, Gender = ProductGender.Male, BasePrice = 350000, Status = ProductStatus.Active, CreatedAt = DateTime.Now.AddDays(-1) },
                "https://images.unsplash.com/photo-1602810318383-e386cc2a3ccf?w=600",
                new List<(string, string, decimal, int)> { ("M", "Trắng", 350000, 25), ("L", "Trắng", 350000, 30), ("XL", "Trắng", 350000, 15) }
            ),
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "ao-phong-oversize-unisex-graphic-print", ProductName = "Áo Phông Oversize Unisex Graphic Print", Description = "Áo thun phông dáng rộng Unisex phong cách Streetwear trẻ trung, cotton 100%.", CategoryId = targetCatAo.CategoryId, BrandId = brandNike.BrandId, Gender = ProductGender.Unisex, BasePrice = 250000, Status = ProductStatus.Active, CreatedAt = DateTime.Now },
                "https://images.unsplash.com/photo-1521572267360-ee0c2909d518?w=600",
                new List<(string, string, decimal, int)> { ("M", "Đen", 250000, 40), ("L", "Đen", 250000, 50), ("XL", "Đen", 250000, 20) }
            ),
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "ao-polo-unisex-premium-pique-cotton", ProductName = "Áo Polo Unisex Premium Pique Cotton", Description = "Áo Polo cổ gấp lịch sự, vải cá sấu pique thoáng khí cao cấp.", CategoryId = targetCatAo.CategoryId, BrandId = brandUniqlo.BrandId, Gender = ProductGender.Unisex, BasePrice = 290000, Status = ProductStatus.Active, CreatedAt = DateTime.Now.AddHours(-5) },
                "https://images.unsplash.com/photo-1586363104862-3a5e2ab60d99?w=600",
                new List<(string, string, decimal, int)> { ("M", "Xanh Navy", 290000, 30), ("L", "Xanh Navy", 290000, 35) }
            ),
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "ao-khoac-blazer-nam-modern-fitting", ProductName = "Áo Khoác Blazer Nam Modern Fitting", Description = "Áo khoác Blazer phong cách Hàn Quốc trẻ trung, phù hợp đi làm và đi tiệc.", CategoryId = targetCatAo.CategoryId, BrandId = brandZara.BrandId, Gender = ProductGender.Male, BasePrice = 650000, Status = ProductStatus.Active, CreatedAt = DateTime.Now.AddDays(-2) },
                "https://images.unsplash.com/photo-1507679799987-c73779587ccf?w=600",
                new List<(string, string, decimal, int)> { ("M", "Xám Đậm", 650000, 12), ("L", "Xám Đậm", 650000, 18) }
            ),
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "quan-jean-slimfit-nam-denim-deep-blue", ProductName = "Quần Jean Slimfit Denim Deep Blue", Description = "Quần Jean nam có độ co giãn nhẹ, bền màu, đường may chắc chắn.", CategoryId = targetCatQuan.CategoryId, BrandId = brandAdidas.BrandId, Gender = ProductGender.Male, BasePrice = 450000, Status = ProductStatus.Active, CreatedAt = DateTime.Now.AddHours(-12) },
                "https://images.unsplash.com/photo-1541099649105-f69ad21f3246?w=600",
                new List<(string, string, decimal, int)> { ("M", "Xanh Chàm", 450000, 20), ("L", "Xanh Chàm", 450000, 25) }
            ),
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "quan-short-the-thao-nike-dynamic", ProductName = "Quần Short Thể Thao Nike Dynamic", Description = "Quần short đùi nam năng động, chất thun lạnh tập gym thể thao cực cool.", CategoryId = targetCatQuan.CategoryId, BrandId = brandNike.BrandId, Gender = ProductGender.Male, BasePrice = 220000, Status = ProductStatus.Active, CreatedAt = DateTime.Now },
                "https://images.unsplash.com/photo-1591195853828-11db59a44f6b?w=600",
                new List<(string, string, decimal, int)> { ("M", "Đen Tuyền", 220000, 50), ("L", "Đen Tuyền", 220000, 45) }
            ),
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "vay-dam-du-tiec-dang-xoe-luxury", ProductName = "Váy Đầm Dự Tiệc Dáng Xòe Luxury", Description = "Đầm nữ dự tiệc sang trọng tôn dáng, may 2 lớp cao cấp.", CategoryId = targetCatVay.CategoryId, BrandId = brandZara.BrandId, Gender = ProductGender.Female, BasePrice = 580000, Status = ProductStatus.Active, CreatedAt = DateTime.Now.AddHours(-2) },
                "https://images.unsplash.com/photo-1595777457583-95e059d581b8?w=600",
                new List<(string, string, decimal, int)> { ("S", "Đỏ Rượu", 580000, 15), ("M", "Đỏ Rượu", 580000, 20) }
            ),
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "vay-midi-floral-vintage-hoa-tiet-hoa", ProductName = "Váy Midi Floral Vintage Họa Tiết Hoa", Description = "Váy hoa nhí dịu dàng thướt tha, chất lụa tơ tằm mềm mại.", CategoryId = targetCatVay.CategoryId, BrandId = brandUniqlo.BrandId, Gender = ProductGender.Female, BasePrice = 420000, Status = ProductStatus.Active, CreatedAt = DateTime.Now.AddDays(-3) },
                "https://images.unsplash.com/photo-1572804013309-59a88b7e92f1?w=600",
                new List<(string, string, decimal, int)> { ("S", "Vàng Nhạt", 420000, 18), ("M", "Vàng Nhạt", 420000, 22) }
            ),
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "mu-luoi-trai-unisex-canvas-signature", ProductName = "Mũ Lưỡi Trai Unisex Canvas Signature", Description = "Nón kết mũ lưỡi trai thêu logo sắc nét, phối đồ phong cách.", CategoryId = targetCatPhuKien.CategoryId, BrandId = brandNike.BrandId, Gender = ProductGender.Unisex, BasePrice = 150000, Status = ProductStatus.Active, CreatedAt = DateTime.Now },
                "https://images.unsplash.com/photo-1588850561407-ed78c282e89b?w=600",
                new List<(string, string, decimal, int)> { ("Freesize", "Đen", 150000, 60), ("Freesize", "Trắng", 150000, 40) }
            ),
            (
                new Product { ProductGuid = Guid.NewGuid(), Slug = "that-lung-nam-da-bo-that-premium", ProductName = "Thắt Lưng Nam Da Bò Thật Premium", Description = "Dây nịt nam khóa kim loại cao cấp, da thật 100% không bong tróc.", CategoryId = targetCatPhuKien.CategoryId, BrandId = brandZara.BrandId, Gender = ProductGender.Male, BasePrice = 320000, Status = ProductStatus.Active, CreatedAt = DateTime.Now.AddDays(-1) },
                "https://images.unsplash.com/photo-1624222247344-550fb60583dc?w=600",
                new List<(string, string, decimal, int)> { ("Standard", "Nâu Đậm", 320000, 30), ("Standard", "Đen", 320000, 25) }
            )
        };

        foreach (var item in productsToSeed)
        {
            if (!await context.Products.AnyAsync(p => p.Slug == item.product.Slug))
            {
                context.Products.Add(item.product);
                await context.SaveChangesAsync();

                context.ProductImages.Add(new ProductImage
                {
                    ProductId = item.product.ProductId,
                    ImageUrl = item.imgUrl,
                    IsMain = true
                });

                foreach (var v in item.variants)
                {
                    context.ProductVariants.Add(new ProductVariant
                    {
                        ProductId = item.product.ProductId,
                        Size = v.size,
                        Color = v.color,
                        SKU = $"{item.product.Slug.Substring(0, 5).ToUpper()}-{v.size}-{v.color}",
                        Price = v.price,
                        StockQuantity = v.stock
                    });
                }

                await context.SaveChangesAsync();
            }
        }
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}
