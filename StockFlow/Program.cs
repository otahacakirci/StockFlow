using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Options;
using StockFlow.Services.Categories;
using StockFlow.Services.Customers;
using StockFlow.Services.Dashboard;
using StockFlow.Services.Orders;
using StockFlow.Services.Products;
using StockFlow.Services.StockMovements;
using StockFlow.Services.Suppliers;

var builder = WebApplication.CreateBuilder(args);
var turkishCulture = CultureInfo.GetCultureInfo("tr-TR");

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Veritabanı bağlantısı yapılandırılmamış. ConnectionStrings:DefaultConnection değerini güvenli yapılandırmada tanımlayın.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(defaultConnection));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = ".StockFlow.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services
    .AddOptions<IdentitySeedOptions>()
    .Bind(builder.Configuration.GetSection(IdentitySeedOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<IdentitySeedOptions>, IdentitySeedOptionsValidator>();
builder.Services.AddScoped<IdentityDataSeeder>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<OrderStockConfirmationPlanner>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderQueryService, OrderQueryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IStockMovementQueryService, StockMovementQueryService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();

builder.Services
    .AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(turkishCulture);
    options.SupportedCultures = [turkishCulture];
    options.SupportedUICultures = [turkishCulture];
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(
        fieldName => $"{fieldName} alanına geçerli bir sayı girin.");
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRequestLocalization();
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets()
    .AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
