using Maranny.Application.DTOs.Bookings;
using Maranny.Application.DTOs.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Maranny.Application.Interfaces
{
    public interface IBookingService
    {
        Task<(bool success, string message, object? data)> BookSessionAsync(int userId, CreateBookingDto dto);
        Task<(bool success, object? data)> GetMyBookingsAsync(int userId, string? status, string? tab, int page, int pageSize);
        Task<(bool success, string message, object? data)> GetBookingDetailsAsync(int userId, int bookingId);
        Task<(bool success, object? data)> GetCoachBookingsAsync(int userId, string? status, string? tab, int page, int pageSize);
        Task<(bool success, string message)> ApproveBookingAsync(int userId, int bookingId);
        Task<(bool success, string message)> DeclineBookingAsync(int userId, int bookingId, CoachBookingActionDto? dto);
        Task<(bool success, string message, object? data)> CancelBookingAsync(int userId, int bookingId, string? reason);
        Task<(bool success, string message, object? data)> CoachCancelSessionAsync(int userId, int sessionId, string? reason);
    }
}