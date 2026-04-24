using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Sessions;

namespace Maranny.Application.Interfaces
{
    public interface ISessionService
    {
        Task<(bool success, string message, object? data)> CreateSessionAsync(int userId, CreateSessionDto dto);
        Task<(bool success, object? data)> GetMySessionsAsync(int userId, string? status, int page, int pageSize);
        Task<object> GetAvailableSessionsAsync(int? coachId, int? sportId, DateTime? date, int page, int pageSize);
        Task<(bool success, string message)> UpdateSessionAsync(int userId, int sessionId, UpdateSessionDto dto);
        Task<(bool success, string message)> CancelSessionAsync(int userId, int sessionId);
    }
}