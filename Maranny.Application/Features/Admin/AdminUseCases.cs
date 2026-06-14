using Maranny.Application.Abstractions.Administration;
using Maranny.Application.DTOs.Admin;

namespace Maranny.Application.Features.Admin
{
    public class AdminUseCases : IAdminUseCases
    {
        private readonly IAdminGateway _adminGateway;

        public AdminUseCases(IAdminGateway adminGateway)
        {
            _adminGateway = adminGateway;
        }

        public Task<(bool success, string message)> BlockUserAsync(int adminId, int userId, BlockUserDto dto) =>
            _adminGateway.BlockUserAsync(adminId, userId, dto);

        public Task<(bool success, string message)> UnblockUserAsync(int userId) =>
            _adminGateway.UnblockUserAsync(userId);

        public Task<object> GetPendingCoachesAsync() => _adminGateway.GetPendingCoachesAsync();

        public Task<(bool success, string message)> VerifyCoachAsync(int adminId, int coachId, VerifyCoachDto dto) =>
            _adminGateway.VerifyCoachAsync(adminId, coachId, dto);

        public Task<(bool success, string message)> RejectCoachAsync(int coachId, RejectCoachDto dto) =>
            _adminGateway.RejectCoachAsync(coachId, dto);

        public Task<(bool success, object? data)> GetUserDetailsAsync(int userId) =>
            _adminGateway.GetUserDetailsAsync(userId);

        public Task<object> GetUsersAsync(string? role, bool? isBlocked, int page, int pageSize) =>
            _adminGateway.GetUsersAsync(role, isBlocked, page, pageSize);

        public Task<object> GetPendingCertificatesAsync() => _adminGateway.GetPendingCertificatesAsync();

        public Task<(bool success, string message)> VerifyCertificateAsync(int adminId, int coachId, string? notes) =>
            _adminGateway.VerifyCertificateAsync(adminId, coachId, notes);

        public Task<object> GetPendingReviewsAsync(int page, int pageSize) =>
            _adminGateway.GetPendingReviewsAsync(page, pageSize);

        public Task<(bool success, string message)> ModerateReviewAsync(int reviewId, string action) =>
            _adminGateway.ModerateReviewAsync(reviewId, action);

        public Task<object> GetAnalyticsAsync() => _adminGateway.GetAnalyticsAsync();
    }
}
