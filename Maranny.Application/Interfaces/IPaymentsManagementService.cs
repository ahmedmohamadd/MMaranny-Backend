using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Payments;

namespace Maranny.Application.Interfaces
{
    public interface IPaymentsManagementService
    {
        Task<(bool success, string message, object? data)> InitiatePaymentAsync(int userId, InitiatePaymentDto dto);
        Task<(bool success, string message, object? data)> GetPaymentDetailsAsync(int userId, int paymentId, bool isAdmin);
        Task<(bool success, object? data)> GetMyPaymentsAsync(int userId);
    }
}