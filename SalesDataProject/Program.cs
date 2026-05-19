using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml; // Import this for ExcelPackage

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

// Configure CORS to allow loopback (localhost) origins and credentials
builder.Services.AddCors(options =>
{
 options.AddPolicy("AllowLocalLoopback", policy =>
 {
 policy.SetIsOriginAllowed(origin =>
 {
 if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
 {
 return uri.IsLoopback;
 }
 return false;
 })
 .AllowAnyHeader()
 .AllowAnyMethod()
 .AllowCredentials();
 });
});

builder.Services.AddSession(options =>
{
 options.IdleTimeout = TimeSpan.FromHours(12); // Extend session timeout to12 hours
 options.Cookie.HttpOnly = true;
 options.Cookie.IsEssential = true;
 // For cross-browser compatibility when embedding or calling from different ports, require None and Secure
 options.Cookie.SameSite = SameSiteMode.None;
 options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Requires HTTPS
});

// OPTIONAL: If using cookie authentication (e.g., Identity or manual cookie auth)
builder.Services.ConfigureApplicationCookie(options =>
{
 options.ExpireTimeSpan = TimeSpan.FromHours(12); // Keep auth cookie alive for12 hours
 options.SlidingExpiration = true; // Reset expiration on each request
 options.Cookie.HttpOnly = true;
 options.Cookie.IsEssential = true;
 options.Cookie.SameSite = SameSiteMode.None;
 options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Requires HTTPS
});

var app = builder.Build();

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
 app.UseExceptionHandler("/Home/Error");
 app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Apply CORS policy for loopback origins
app.UseCors("AllowLocalLoopback");

app.UseSession(); // Use session after routing and before auth

app.UseAuthentication(); // Add if you're using authentication
app.UseAuthorization();

app.MapControllerRoute(
 name: "default",
 pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
