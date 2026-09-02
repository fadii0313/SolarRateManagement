using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Infrastructure.Data;
using SolarRateManagement.Infrastructure.Services;
using System;
using System.IO;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    var env = hostingContext.HostingEnvironment;
    config.Sources.Clear();
    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
          .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false)
          .AddEnvironmentVariables();
});

// Add services to the container.
builder.Services.AddControllers();

// Register DbContext dynamically supporting SQL Server (LocalDB) and PostgreSQL (Production / Cloud)
var connString = GetFormattedConnectionString(builder.Configuration);
var isPostgres = connString.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
                 connString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                 builder.Configuration.GetValue<bool>("UsePostgres");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (isPostgres)
    {
        options.UseNpgsql(connString, b => b.MigrationsAssembly("SolarRateManagement.Infrastructure"));
    }
    else
    {
        options.UseSqlServer(connString, b => b.MigrationsAssembly("SolarRateManagement.Infrastructure"));
    }
});

// Register Token Service
builder.Services.AddTransient<ITokenService, TokenService>();

// Register HttpContextAccessor and Shop Context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IShopContext, ShopContext>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var keyStr = jwtSettings["SecurityKey"] ?? "SolarRateManagementSystemAdvancedSuperSecretKey123!";
var issuer = jwtSettings["Issuer"] ?? "SolarRateManagementAPI";
var audience = jwtSettings["Audience"] ?? "SolarRateManagementUI";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr))
    };
});

// Configure CORS to allow Angular SPA requests locally & production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<SolarRateManagement.API.Middleware.GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAngularApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Initialize and seed database tables in all environments (Development & Production)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        DbInitializer.Seed(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing or seeding the database: {Message}", ex.Message);
    }
}

app.Run();

static string GetFormattedConnectionString(IConfiguration configuration)
{
    var conn = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(conn))
    {
        conn = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "";
    }

    if (string.IsNullOrWhiteSpace(conn)) return conn;

    // Convert postgres:// or postgresql:// URI format if present
    if (conn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        conn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(conn);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var db = uri.AbsolutePath.TrimStart('/');

            return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;";
        }
        catch
        {
            return conn;
        }
    }

    return conn;
}
