using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Administration;
using Maranny.Application.Abstractions.Identity;
using Maranny.Application.Abstractions.Messaging;
using Maranny.Application.Abstractions.Notifications;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Abstractions.Profiles;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Infrastructure.Common;
using Maranny.Infrastructure.Data;
using Maranny.Infrastructure.Persistence;
using Maranny.Infrastructure.Persistence.ReadRepositories;
using Maranny.Infrastructure.Persistence.Repositories;
using Maranny.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maranny.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IAdminGateway, AdminService>();
            services.AddScoped<IUserProfileGateway, UsersService>();
            services.AddScoped<IAuthGateway, AuthService>();
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IEmailValidationService, EmailValidationService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<INotificationGateway, NotificationService>();
            services.AddHttpClient<IPaymentService, PaymentService>();
            services.AddScoped<IChatGateway, ChatService>();
            services.AddSingleton<IClock, SystemClock>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IClientRepository, ClientRepository>();
            services.AddScoped<ICoachRepository, CoachRepository>();
            services.AddScoped<ITrainingSessionRepository, TrainingSessionRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IBookingReadRepository, BookingReadRepository>();
            services.AddScoped<ICoachSportRepository, CoachSportRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<ISportRepository, SportRepository>();
            services.AddScoped<ISessionRepository, SessionRepository>();
            services.AddScoped<ISessionReadRepository, SessionReadRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IProductReadRepository, ProductReadRepository>();
            services.AddScoped<IReviewRepository, ReviewRepository>();
            services.AddScoped<IReviewReadRepository, ReviewReadRepository>();
            services.AddScoped<IPaymentReadRepository, PaymentReadRepository>();
            services.AddScoped<ISearchReadRepository, SearchReadRepository>();

            services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;

                options.User.RequireUniqueEmail = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            services.AddSignalR();

            return services;
        }
    }
}
