using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SolarRateManagement.Application.Common.Interfaces;
using SolarRateManagement.Infrastructure.Data;
using SolarRateManagement.Infrastructure.Services;
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
var connString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
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

// Seeding database in Development environment
if (app.Environment.IsDevelopment())
{
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
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }
}

app.Run();
