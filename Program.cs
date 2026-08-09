using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using FashionStore.Web.Services;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Hubs;
using WEBBANQUANAO.Services;

// Bật legacy timestamp behavior cho Npgsql (tránh lỗi xung đột timezone giữa C# DateTime và PostgreSQL timestamp)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. CẤU HÌNH CƠ SỞ DỮ LIỆU (POSTGRESQL HOẶC SQL SERVER)
// =========================================================================
var defaultConnStr = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["DATABASE_URL"] 
    ?? "";

// Ưu tiên sử dụng Connection String trực tiếp nếu được cấp qua Biến Môi Trường (Render Environment Variables)
if (string.IsNullOrEmpty(defaultConnStr))
{
    var activeSection = builder.Environment.IsDevelopment() ? "development" : "production";
    var pgHost = builder.Configuration[$"{activeSection}:host"] 
        ?? builder.Configuration["production:host"] 
        ?? builder.Configuration["development:host"] 
        ?? builder.Configuration["PostgreSQL:Host"];

    if (!string.IsNullOrEmpty(pgHost))
    {
        var pgUser = builder.Configuration[$"{activeSection}:username"] 
            ?? builder.Configuration["production:username"] 
            ?? builder.Configuration["development:username"] 
            ?? builder.Configuration["PostgreSQL:Username"] ?? "vi";
            
        var pgPass = builder.Configuration[$"{activeSection}:password"] 
            ?? builder.Configuration["production:password"] 
            ?? builder.Configuration["development:password"] 
            ?? builder.Configuration["PostgreSQL:Password"] ?? "";
            
        var pgDb = builder.Configuration[$"{activeSection}:database"] 
            ?? builder.Configuration["production:database"] 
            ?? builder.Configuration["development:database"] 
            ?? builder.Configuration["PostgreSQL:Database"] ?? "fashionstore_1y94";
            
        var pgPort = builder.Configuration[$"{activeSection}:port"] 
            ?? builder.Configuration["production:port"] 
            ?? builder.Configuration["development:port"] 
            ?? builder.Configuration["PostgreSQL:Port"] ?? "5432";
            
        defaultConnStr = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass};SSL Mode=Require;Trust Server Certificate=true;";
    }
}

// Chuyển đổi định dạng postgres:// hoặc postgresql:// nếu Render cấp dạng URL
if (defaultConnStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || 
    defaultConnStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var uri = new Uri(defaultConnStr);
        var userInfo = uri.UserInfo.Split(':');
        var user = userInfo[0];
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var dbName = uri.AbsolutePath.TrimStart('/');
        defaultConnStr = $"Host={host};Port={port};Database={dbName};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
    }
    catch { }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (defaultConnStr.Contains("Host=", StringComparison.OrdinalIgnoreCase) || 
        defaultConnStr.Contains("Postgres", StringComparison.OrdinalIgnoreCase) || 
        defaultConnStr.Contains("postgres://", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(defaultConnStr);
    }
    else
    {
        options.UseSqlServer(defaultConnStr);
    }
});

// =========================================================================
// 2. DỊCH VỤ DỰ ÁN (SERVICES)
// =========================================================================
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAprioriService, AprioriService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

// =========================================================================
// 3. XÁC THỰC NGƯỜI DÙNG (AUTHENTICATION - COOKIE, GOOGLE & FACEBOOK)
// =========================================================================
var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

string googleClientId = builder.Configuration["GoogleAuth:ClientId"] ?? "";
string googleClientSecret = builder.Configuration["GoogleAuth:ClientSecret"] ?? "";
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

string fbAppId = builder.Configuration["FacebookAuth:AppId"] ?? "";
string fbAppSecret = builder.Configuration["FacebookAuth:AppSecret"] ?? "";
if (!string.IsNullOrEmpty(fbAppId) && !string.IsNullOrEmpty(fbAppSecret))
{
    authBuilder.AddFacebook(options =>
    {
        options.AppId = fbAppId;
        options.AppSecret = fbAppSecret;
        options.Fields.Add("email");
        options.Fields.Add("name");
        options.Fields.Add("picture");
    });
}

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// =========================================================================
// 4. KHỞI TẠO PIPELINE & MIDDLEWARE
// =========================================================================
var app = builder.Build();

// Auto Database Initialization & Migration Check
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DbInitializer.SeedAsync(context);

        if (context.Database.IsSqlServer())
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserBehaviorLogs]') AND name = 'DeviceType')
                        ALTER TABLE [UserBehaviorLogs] ADD [DeviceType] NVARCHAR(20) NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserBehaviorLogs]') AND name = 'DwellTimeSeconds')
                        ALTER TABLE [UserBehaviorLogs] ADD [DwellTimeSeconds] FLOAT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserBehaviorLogs]') AND name = 'IpAddress')
                        ALTER TABLE [UserBehaviorLogs] ADD [IpAddress] NVARCHAR(50) NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserBehaviorLogs]') AND name = 'IsRageClick')
                        ALTER TABLE [UserBehaviorLogs] ADD [IsRageClick] BIT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserBehaviorLogs]') AND name = 'PageUrl')
                        ALTER TABLE [UserBehaviorLogs] ADD [PageUrl] NVARCHAR(255) NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserBehaviorLogs]') AND name = 'RecommendationBlockId')
                        ALTER TABLE [UserBehaviorLogs] ADD [RecommendationBlockId] NVARCHAR(50) NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserBehaviorLogs]') AND name = 'RecommendationSource')
                        ALTER TABLE [UserBehaviorLogs] ADD [RecommendationSource] NVARCHAR(50) NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserBehaviorLogs]') AND name = 'SearchQuery')
                        ALTER TABLE [UserBehaviorLogs] ADD [SearchQuery] NVARCHAR(200) NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserBehaviorLogs]') AND name = 'ProductId' AND is_nullable = 0)
                        ALTER TABLE [UserBehaviorLogs] ALTER COLUMN [ProductId] INT NULL;
                ");
            }
            catch { }
        }
    }
    catch { }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// =========================================================================
// 5. ĐỊNH TUYẾN (ROUTES)
// =========================================================================

// Route biệt lập cho Admin Area
app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "sys-admin-management/{controller=Dashboard}/{action=Index}/{id?}");

// Route tự động tạo CSDL nếu gọi /createTables
app.MapGet("/createTables", async (ApplicationDbContext context) =>
{
    try
    {
        await context.Database.EnsureCreatedAsync();
        await DbInitializer.SeedAsync(context);
        return Results.Text("tables created!");
    }
    catch (Exception ex)
    {
        return Results.Text($"Error creating tables: {ex.Message}");
    }
});

// Route mặc định cho Khách hàng
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<StockHub>("/hubs/stock");

app.Run();
