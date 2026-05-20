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

// Configure max upload sizes
builder.Services.Configure<FormOptions>(options =>
{
 options.MultipartBodyLengthLimit =200 *1024 *1024; //200 MB
 options.ValueLengthLimit = int.MaxValue;
 options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Configure Kestrel max request body (optional)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
 serverOptions.Limits.MaxRequestBodySize =200 *1024 *1024; //200 MB
});

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
 options.IdleTimeout = TimeSpan.FromHours(12); // Extend session timeout
 options.Cookie.HttpOnly = true;
 options.Cookie.IsEssential = true;
 // Use SameAsRequest so Secure cookie requirement follows the request scheme (helps non-HTTPS deployments)
 options.Cookie.SameSite = SameSiteMode.Lax; // Lax is safe and compatible with most browsing flows
 options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Accept both HTTP and HTTPS depending on request
});

// OPTIONAL: If using cookie authentication (e.g., Identity or manual cookie auth)
builder.Services.ConfigureApplicationCookie(options =>
{
 options.ExpireTimeSpan = TimeSpan.FromHours(12);
 options.SlidingExpiration = true;
 options.Cookie.HttpOnly = true;
 options.Cookie.IsEssential = true;
 options.Cookie.SameSite = SameSiteMode.Lax;
 options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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

app.UseSession(); // Session before authentication/authorization

app.UseAuthentication(); // Add if you're using authentication
app.UseAuthorization();

app.MapControllerRoute(
 name: "default",
 pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
