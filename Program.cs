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
