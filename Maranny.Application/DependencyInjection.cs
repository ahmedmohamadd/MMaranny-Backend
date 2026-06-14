using Maranny.Application.Features.Bookings.ApproveBooking;
using Maranny.Application.Features.Bookings.BookSession;
using Maranny.Application.Features.Bookings.CancelBooking;
using Maranny.Application.Features.Bookings.CoachCancelSession;
using Maranny.Application.Features.Bookings.DeclineBooking;
using Maranny.Application.Features.Bookings.GetBookingDetails;
using Maranny.Application.Features.Bookings.GetCoachBookings;
using Maranny.Application.Features.Bookings.GetMyBookings;
using Maranny.Application.Features.Sports.CreateSport;
using Maranny.Application.Features.Sports.GetSports;
using Maranny.Application.Features.Sessions.CancelSession;
using Maranny.Application.Features.Sessions.CreateSession;
using Maranny.Application.Features.Sessions.GetAvailableSessions;
using Maranny.Application.Features.Sessions.GetMySessions;
using Maranny.Application.Features.Sessions.UpdateSession;
using Maranny.Application.Features.Products.CreateProduct;
using Maranny.Application.Features.Products.DeleteProduct;
using Maranny.Application.Features.Products.GetProductDetails;
using Maranny.Application.Features.Products.GetProducts;
using Maranny.Application.Features.Products.UpdateProduct;
using Maranny.Application.Features.Reviews.GetCoachReviews;
using Maranny.Application.Features.Reviews.RespondToReview;
using Maranny.Application.Features.Reviews.SubmitReview;
using Maranny.Application.Features.Payments.GetMyPayments;
using Maranny.Application.Features.Payments.GetPaymentDetails;
using Maranny.Application.Features.Payments.InitiatePayment;
using Maranny.Application.Features.Search.GetCoachDetails;
using Maranny.Application.Features.Search.SearchCoaches;
using Maranny.Application.Features.Admin;
using Maranny.Application.Features.Auth;
using Maranny.Application.Features.Chat;
using Maranny.Application.Features.Notifications;
using Maranny.Application.Features.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Maranny.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IBookSessionUseCase, BookSessionUseCase>();
            services.AddScoped<IApproveBookingUseCase, ApproveBookingUseCase>();
            services.AddScoped<IDeclineBookingUseCase, DeclineBookingUseCase>();
            services.AddScoped<ICancelBookingUseCase, CancelBookingUseCase>();
            services.AddScoped<ICoachCancelSessionUseCase, CoachCancelSessionUseCase>();
            services.AddScoped<IGetMyBookingsUseCase, GetMyBookingsUseCase>();
            services.AddScoped<IGetBookingDetailsUseCase, GetBookingDetailsUseCase>();
            services.AddScoped<IGetCoachBookingsUseCase, GetCoachBookingsUseCase>();
            services.AddScoped<IGetSportsUseCase, GetSportsUseCase>();
            services.AddScoped<ICreateSportUseCase, CreateSportUseCase>();
            services.AddScoped<ICreateSessionUseCase, CreateSessionUseCase>();
            services.AddScoped<IGetMySessionsUseCase, GetMySessionsUseCase>();
            services.AddScoped<IGetAvailableSessionsUseCase, GetAvailableSessionsUseCase>();
            services.AddScoped<IUpdateSessionUseCase, UpdateSessionUseCase>();
            services.AddScoped<ICancelSessionUseCase, CancelSessionUseCase>();
            services.AddScoped<ICreateProductUseCase, CreateProductUseCase>();
            services.AddScoped<IGetProductsUseCase, GetProductsUseCase>();
            services.AddScoped<IGetProductDetailsUseCase, GetProductDetailsUseCase>();
            services.AddScoped<IUpdateProductUseCase, UpdateProductUseCase>();
            services.AddScoped<IDeleteProductUseCase, DeleteProductUseCase>();
            services.AddScoped<ISubmitReviewUseCase, SubmitReviewUseCase>();
            services.AddScoped<IGetCoachReviewsUseCase, GetCoachReviewsUseCase>();
            services.AddScoped<IRespondToReviewUseCase, RespondToReviewUseCase>();
            services.AddScoped<IInitiatePaymentUseCase, InitiatePaymentUseCase>();
            services.AddScoped<IGetPaymentDetailsUseCase, GetPaymentDetailsUseCase>();
            services.AddScoped<IGetMyPaymentsUseCase, GetMyPaymentsUseCase>();
            services.AddScoped<ISearchCoachesUseCase, SearchCoachesUseCase>();
            services.AddScoped<IGetCoachDetailsUseCase, GetCoachDetailsUseCase>();
            services.AddScoped<IAuthUseCases, AuthUseCases>();
            services.AddScoped<IAdminUseCases, AdminUseCases>();
            services.AddScoped<IUserUseCases, UserUseCases>();
            services.AddScoped<INotificationUseCases, NotificationUseCases>();
            services.AddScoped<IChatUseCases, ChatUseCases>();

            return services;
        }
    }
}
