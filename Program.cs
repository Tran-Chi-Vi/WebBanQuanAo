using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using FashionStore.Web.Services;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Hubs;
using WEBBANQUANAO.Services;

var builder = WebApplication.CreateBuilder(args);

// DB CONTEXT
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// HTTP CLIENT & SERVICES
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAprioriService, AprioriService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

// AUTHENTICATION (COOKIE, GOOGLE & FACEBOOK)
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

// MVC & SIGNALR
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

// AUTO SEED DATA & DATABASE INITIALIZATION
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbInitializer.SeedAsync(context);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Auto Schema Migration for UserBehaviorLogs columns
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        db.Database.ExecuteSqlRaw(@"
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Route biệt lập 100% cho Admin Area
app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "sys-admin-management/{controller=Dashboard}/{action=Index}/{id?}");

// Route chuẩn cho Khách hàng
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<StockHub>("/hubs/stock");

app.Run();
