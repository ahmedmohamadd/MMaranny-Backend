using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace Maranny.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ===== DATABASE =====
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions => sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null)
                )
            );

            // Register JWT Service
            builder.Services.AddScoped<Maranny.Core.Interfaces.IJwtService, Maranny.Infrastructure.Services.JwtService>();

            // Register Email Validation Service
            builder.Services.AddScoped<Maranny.Core.Interfaces.IEmailValidationService, Maranny.Infrastructure.Services.EmailValidationService>();

            // Register Email Service (not configured yet - will add later)
            builder.Services.AddScoped<Maranny.Core.Interfaces.IEmailService, Maranny.Infrastructure.Services.EmailService>();

            // Register Notification Service
            builder.Services.AddScoped<Maranny.Core.Interfaces.INotificationService, Maranny.Infrastructure.Services.NotificationService>();

            // Register HttpClient for PaymentService
            builder.Services.AddHttpClient<Maranny.Core.Interfaces.IPaymentService, Maranny.Infrastructure.Services.PaymentService>();

            // Register Chat Service
            builder.Services.AddScoped<Maranny.Core.Interfaces.IChatService, Maranny.Infrastructure.Services.ChatService>();

            // ===== IDENTITY =====
            builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
            {
                // Password settings
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;

                // User settings
                options.User.RequireUniqueEmail = true;

                // Lockout settings
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // ===== JWT AUTHENTICATION =====
            var jwtSettings = builder.Configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"]!;

            var authenticationBuilder = builder.Services.AddAuthentication(options =>
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
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secretKey)
                    ),
                    ClockSkew = TimeSpan.Zero
                };
            });

            var googleClientId = builder.Configuration["GoogleAuth:ClientId"];
            var googleClientSecret = builder.Configuration["GoogleAuth:ClientSecret"];
            if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
            {
                authenticationBuilder.AddGoogle(options =>
                {
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.CallbackPath = "/signin-google";
                });
            }

            // ===== AUTHORIZATION =====
            builder.Services.AddAuthorization();

            // ===== CONTROLLERS =====
            builder.Services.AddControllers();

            // ===== SWAGGER WITH JWT SUPPORT =====
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace("+", "."));
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Maranny API",
                    Version = "v1",
                    Description = "Sports Coaching Platform API"
                });

                // Add JWT Authentication to Swagger
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter: Bearer {your JWT token}"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // ===== CORS (for Flutter) =====
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });

                options.AddPolicy("SignalRPolicy", policy =>
                {
                    policy.WithOrigins("http://localhost:3000", "https://yourdomain.com") // Add your Flutter app URLs
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });
            // Add SignalR
            builder.Services.AddSignalR();

            var app = builder.Build();

            // Seed roles and default admin
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    await SeedRolesAndAdmin(services);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while seeding the database.");
                }
            }

            // ===== MIDDLEWARE PIPELINE =====
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();
            app.UseStaticFiles(); // Enable serving static files from wwwroot
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseMiddleware<Maranny.Infrastructure.Middleware.BlockedUserMiddleware>();
            app.UseAuthorization();
            app.MapHub<Maranny.Infrastructure.Hubs.NotificationHub>("/notificationHub");
            app.MapHub<Maranny.Infrastructure.Hubs.ChatHub>("/chatHub");
            app.MapControllers();
            app.Run();
        }

        static async Task SeedRolesAndAdmin(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Coaches', 'Age') IS NULL
BEGIN
    ALTER TABLE dbo.Coaches ADD Age int NULL;
END

IF COL_LENGTH('dbo.Coaches', 'AvailabilityStatus') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Coaches ALTER COLUMN AvailabilityStatus nvarchar(max) NULL;
END

IF COL_LENGTH('dbo.Products', 'ListingLocation') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD ListingLocation nvarchar(200) NULL;
END

IF COL_LENGTH('dbo.Products', 'ShowPhoneNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD ShowPhoneNumber bit NOT NULL CONSTRAINT DF_Products_ShowPhoneNumber DEFAULT(1);
END

IF COL_LENGTH('dbo.Clients', 'Bio') IS NULL
BEGIN
    ALTER TABLE dbo.Clients ADD Bio nvarchar(1000) NULL;
END");

            // Seed Roles
            string[] roles = { "Admin", "Coach", "Client" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(role));
                }
            }

            var defaultAdmins = new[]
            {
                new
                {
                    Email = "admin@maranny.com",
                    Password = "Admin@123456",
                    FirstName = "System",
                    LastName = "Admin",
                    Username = "admin"
                },
                new
                {
                    Email = "admin1@maranny.com",
                    Password = "Admin@123456",
                    FirstName = "Maranny",
                    LastName = "Admin One",
                    Username = "admin1"
                },
                new
                {
                    Email = "admin2@maranny.com",
                    Password = "Admin@123456",
                    FirstName = "Maranny",
                    LastName = "Admin Two",
                    Username = "admin2"
                }
            };

            foreach (var seedAdmin in defaultAdmins)
            {
                var adminUser = await userManager.FindByEmailAsync(seedAdmin.Email);

                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        Email = seedAdmin.Email,
                        UserName = seedAdmin.Email,
                        EmailConfirmed = true,
                        PhoneNumberConfirmed = true,
                        PrimaryUserType = UserType.Admin,
                        LockoutEnabled = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(adminUser, seedAdmin.Password);
                    if (!result.Succeeded)
                    {
                        continue;
                    }
                }

                adminUser.EmailConfirmed = true;
                adminUser.PhoneNumberConfirmed = true;
                adminUser.PrimaryUserType = UserType.Admin;
                adminUser.LockoutEnabled = true;
                await userManager.UpdateAsync(adminUser);

                if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }

                var adminProfile = await dbContext.Admins
                    .FirstOrDefaultAsync(a => a.UserId == adminUser.Id);

                if (adminProfile == null)
                {
                    dbContext.Admins.Add(new Admin
                    {
                        UserId = adminUser.Id,
                        F_name = seedAdmin.FirstName,
                        L_name = seedAdmin.LastName,
                        Email = seedAdmin.Email,
                        Password = "",
                        Username = seedAdmin.Username
                    });
                }
                else
                {
                    adminProfile.F_name = seedAdmin.FirstName;
                    adminProfile.L_name = seedAdmin.LastName;
                    adminProfile.Email = seedAdmin.Email;
                    adminProfile.Username = seedAdmin.Username;
                }
            }

            await dbContext.SaveChangesAsync();
        }

    }

}
