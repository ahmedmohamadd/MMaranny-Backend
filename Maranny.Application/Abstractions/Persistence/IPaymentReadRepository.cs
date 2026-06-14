namespace Maranny.Application.Abstractions.Persistence
{
    public interface IPaymentReadRepository
    {
        Task<(string? error, object? data)> GetPaymentDetailsAsync(int userId, int paymentId, bool isAdmin);
        Task<object?> GetClientPaymentsAsync(int userId);
    }
}
