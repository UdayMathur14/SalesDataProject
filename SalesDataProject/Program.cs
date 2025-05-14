using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;  // Import this for ExcelPackage

var builder = WebApplication.CreateBuilder(args);

// Set EPPlus license context for non-commercial use
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// Add services to the container
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Enable in-memory session storage
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12); // Extend session timeout to 12 hours
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax; // Helps with cookie persistence
});

// OPTIONAL: If using cookie authentication (e.g., Identity or manual cookie auth)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(12); // Keep auth cookie alive for 12 hours
    options.SlidingExpiration = true;                // Reset expiration on each request
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSession(); // Make sure this comes BEFORE Routing/Authorization

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Add if you're using authentication
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
