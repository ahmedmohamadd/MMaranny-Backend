using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Enums;

namespace Maranny.Application.Interfaces
{
    public interface INotificationService
    {
        Task SendNotificationAsync(int userId, string title, string message, NotificationType type);
        Task SendNotificationToMultipleUsersAsync(List<int> userIds, string title, string message, NotificationType type);
        Task<List<object>> GetUserNotificationsAsync(int userId, bool unreadOnly = false);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task<int> GetUnreadCountAsync(int userId);
    }
}