using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Reviews;

namespace Maranny.Application.Interfaces
{
    public interface IReviewService
    {
        Task<(bool success, string message, object? data)> SubmitReviewAsync(int userId, SubmitReviewDto dto);
        Task<(bool success, object? data)> GetCoachReviewsAsync(int coachId, int page, int pageSize);
        Task<(bool success, string message)> RespondToReviewAsync(int userId, int reviewId, CoachResponseDto dto);
    }
}