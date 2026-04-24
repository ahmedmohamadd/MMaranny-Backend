using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Admin;

namespace Maranny.Application.Interfaces
{
    public interface IAdminService
    {
        Task<(bool success, string message)> BlockUserAsync(int adminId, int userId, BlockUserDto dto);
        Task<(bool success, string message)> UnblockUserAsync(int userId);
        Task<object> GetPendingCoachesAsync();
        Task<(bool success, string message)> VerifyCoachAsync(int adminId, int coachId, VerifyCoachDto dto);
        Task<(bool success, string message)> RejectCoachAsync(int coachId, RejectCoachDto dto);
        Task<(bool success, object? data)> GetUserDetailsAsync(int userId);
        Task<object> GetUsersAsync(string? role, bool? isBlocked, int page, int pageSize);
        Task<object> GetPendingCertificatesAsync();
        Task<(bool success, string message)> VerifyCertificateAsync(int adminId, int coachId, string? notes);
        Task<object> GetPendingReviewsAsync(int page, int pageSize);
        Task<(bool success, string message)> ModerateReviewAsync(int reviewId, string action);
        Task<object> GetAnalyticsAsync();
    }
}