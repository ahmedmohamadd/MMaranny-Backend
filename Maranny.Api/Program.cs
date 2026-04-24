using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Maranny.Infrastructure.Services;
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
                    builder.Configuration.GetConnectionString("DefaultConnection")
                )
            );

            builder.Services.AddScoped<IAdminService, AdminService>();

            builder.Services.AddScoped<IUserService, UsersService>();

            builder.Services.AddScoped<IPaymentsManagementService, PaymentsManagementService>();

            builder.Services.AddScoped<IBookingService, BookingsService>();

            builder.Services.AddScoped<ISessionService, SessionsService>();

            builder.Services.AddScoped<ISearchService, SearchService>();

            builder.Services.AddScoped<IReviewService, ReviewsService>();

            builder.Services.AddScoped<IProductService, ProductsService>();

            builder.Services.AddScoped<ISportsService, SportsService>();

            builder.Services.AddScoped<IAuthService, AuthService>();

            // Register JWT Service
            builder.Services.AddScoped<Maranny.Application.Interfaces.IJwtService, Maranny.Infrastructure.Services.JwtService>();

            // Register Email Validation Service
            builder.Services.AddScoped<Maranny.Application.Interfaces.IEmailValidationService, Maranny.Infrastructure.Services.EmailValidationService>();

            // Register Email Service (not configured yet - will add later)
            builder.Services.AddScoped<Maranny.Application.Interfaces.IEmailService, Maranny.Infrastructure.Services.EmailService>();

            // Register Notification Service
            builder.Services.AddScoped<Maranny.Application.Interfaces.INotificationService, Maranny.Infrastructure.Services.NotificationService>();

            // Register HttpClient for PaymentService
            builder.Services.AddHttpClient<Maranny.Application.Interfaces.IPaymentService, Maranny.Infrastructure.Services.PaymentService>();

            // Register Chat Service
            builder.Services.AddScoped<Maranny.Application.Interfaces.IChatService, Maranny.Infrastructure.Services.ChatService>();

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
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // ===== JWT AUTHENTICATION =====
            var jwtSettings = builder.Configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"]!;

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
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secretKey)
                    ),
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddGoogle(options =>
            {
                options.ClientId = builder.Configuration["GoogleAuth:ClientId"]!;
                options.ClientSecret = builder.Configuration["GoogleAuth:ClientSecret"]!;
                options.CallbackPath = "/signin-google";
            });

            // ===== AUTHORIZATION =====
            builder.Services.AddAuthorization();

            // ===== CONTROLLERS =====
            builder.Services.AddControllers();

            // ===== SWAGGER WITH JWT SUPPORT =====
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
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
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles(); // Enable serving static files from wwwroot
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseMiddleware<Maranny.Api.Middleware.BlockedUserMiddleware>();
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

            // Seed Roles
            string[] roles = { "Admin", "Coach", "Client" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(role));
                }
            }

            // Seed default Admin user
            var adminEmail = "admin@maranny.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    Email = adminEmail,
                    UserName = adminEmail,
                    EmailConfirmed = true,
                    PrimaryUserType = UserType.Admin,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123456");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");

                    // Create Admin profile
                    var admin = new Admin
                    {
                        UserId = adminUser.Id,
                        F_name = "System",
                        L_name = "Admin",
                        Email = adminEmail,
                        Password = "", // Not used
                        Username = "admin"
                    };
                    dbContext.Admins.Add(admin);
                    await dbContext.SaveChangesAsync();
                }
            }
        }

    }

}