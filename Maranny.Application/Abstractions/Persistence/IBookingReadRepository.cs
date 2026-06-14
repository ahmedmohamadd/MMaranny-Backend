namespace Maranny.Application.Abstractions.Persistence
{
    public interface IBookingReadRepository
    {
        Task<object?> GetClientBookingsAsync(
            int userId,
            string? status,
            string? tab,
            int page,
            int pageSize);

        Task<(string? error, object? data)> GetClientBookingDetailsAsync(int userId, int bookingId);

        Task<object?> GetCoachBookingsAsync(
            int userId,
            string? status,
            string? tab,
            int page,
            int pageSize);
    }
}
