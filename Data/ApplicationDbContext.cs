using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Models.Entities;
namespace WEBBANQUANAO.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // ==== Người dùng & Xác thực ====
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();

    // ==== Danh mục & Sản phẩm ====
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    // ==== Giỏ hàng & Đơn hàng ====
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<Payment> Payments => Set<Payment>();

    // ==== Tương tác người dùng ====
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<UserBehaviorLog> UserBehaviorLogs => Set<UserBehaviorLog>();
    public DbSet<AssociationRule> AssociationRules => Set<AssociationRule>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssociationRule>()
            .HasKey(x => x.RuleId);
        // ============================================================
        // 1. RÀNG BUỘC UNIQUE
        // ============================================================
        modelBuilder.Entity<Role>()
            .HasIndex(r => r.RoleName).IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.GoogleId).IsUnique();

        modelBuilder.Entity<Brand>()
            .HasIndex(b => b.BrandName).IsUnique();

        modelBuilder.Entity<ProductVariant>()
            .HasIndex(v => v.SKU).IsUnique();

        modelBuilder.Entity<Promotion>()
            .HasIndex(p => p.Code).IsUnique();

        // Khóa ghép logic (UserId, ProductId) — 1 người chỉ yêu thích 1 sản phẩm 1 lần
        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.ProductId }).IsUnique();

        // ============================================================
        // 2. QUAN HỆ - NGƯỜI DÙNG & XÁC THỰC
        // ============================================================
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Address>()
            .HasOne(a => a.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ============================================================
        // 3. QUAN HỆ - DANH MỤC & SẢN PHẨM
        // ============================================================
        // Category tự tham chiếu (cây danh mục)
        modelBuilder.Entity<Category>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductVariant>()
            .HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductImage>()
            .HasOne(i => i.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // ============================================================
        // 4. QUAN HỆ - GIỎ HÀNG & ĐƠN HÀNG
        // ============================================================
        modelBuilder.Entity<Cart>()
            .HasOne(c => c.User)
            .WithOne(u => u.Cart)
            .HasForeignKey<Cart>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CartItem>()
            .HasOne(ci => ci.Cart)
            .WithMany(c => c.Items)
            .HasForeignKey(ci => ci.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CartItem>()
            .HasOne(ci => ci.Variant)
            .WithMany(v => v.CartItems)
            .HasForeignKey(ci => ci.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict); // Giữ lịch sử đơn hàng

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Address)
            .WithMany()
            .HasForeignKey(o => o.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Promotion)
            .WithMany(p => p.Orders)
            .HasForeignKey(o => o.PromotionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrderDetail>()
            .HasOne(od => od.Order)
            .WithMany(o => o.OrderDetails)
            .HasForeignKey(od => od.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderDetail>()
            .HasOne(od => od.Variant)
            .WithMany(v => v.OrderDetails)
            .HasForeignKey(od => od.VariantId)
            .OnDelete(DeleteBehavior.Restrict); // Giữ lịch sử, không cho xóa Variant đã bán

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Order)
            .WithOne(o => o.Payment)
            .HasForeignKey<Payment>(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================================
        // 5. QUAN HỆ - TƯƠNG TÁC NGƯỜI DÙNG
        // ============================================================
        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.User)
            .WithMany(u => u.Favorites)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.Product)
            .WithMany(p => p.Favorites)
            .HasForeignKey(f => f.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Product)
            .WithMany(p => p.Reviews)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserBehaviorLog>()
            .HasOne(l => l.User)
            .WithMany(u => u.BehaviorLogs)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull); // NULL nếu khách vãng lai / user bị xóa

        modelBuilder.Entity<UserBehaviorLog>()
            .HasOne(l => l.Product)
            .WithMany(p => p.BehaviorLogs)
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // AssociationRule: Product tham gia 2 vai trò Antecedent/Consequent
        // -> phải tắt cascade cả 2 để tránh multiple cascade paths trên SQL Server
        modelBuilder.Entity<AssociationRule>()
            .HasOne(ar => ar.AntecedentProduct)
            .WithMany(p => p.RulesAsAntecedent)
            .HasForeignKey(ar => ar.AntecedentProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AssociationRule>()
            .HasOne(ar => ar.ConsequentProduct)
            .WithMany(p => p.RulesAsConsequent)
            .HasForeignKey(ar => ar.ConsequentProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatConversation>()
            .HasOne(cc => cc.User)
            .WithMany(u => u.ChatConversations)
            .HasForeignKey(cc => cc.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // ============================================================
        // 6. DỮ LIỆU MẪU CHO ROLE (Seed data)
        // ============================================================
        modelBuilder.Entity<Role>().HasData(
            new Role { RoleId = 1, RoleName = "Admin" },
            new Role { RoleId = 2, RoleName = "Customer" }
        );
    }
}
