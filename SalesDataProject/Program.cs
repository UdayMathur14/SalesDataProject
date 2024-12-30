using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;  // Import this for ExcelPackage

var builder = WebApplication.CreateBuilder(args);

// Set EPPlus license context for non-commercial use (LGPL license)
ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // Or LicenseContext.Commercial if you have a commercial license

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(300); // Set timeout as needed
    options.Cookie.HttpOnly = true; // Protects the session cookie
    options.Cookie.IsEssential = true; // Required for session to work
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
