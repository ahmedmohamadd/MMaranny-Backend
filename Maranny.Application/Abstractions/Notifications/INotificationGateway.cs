using Maranny.Core.Enums;

namespace Maranny.Application.Abstractions.Notifications
{
    public interface INotificationGateway
    {
        Task SendNotificationAsync(int userId, string title, string message, NotificationType type);
        Task SendNotificationToMultipleUsersAsync(List<int> userIds, string title, string message, NotificationType type);
        Task<List<object>> GetUserNotificationsAsync(int userId, bool unreadOnly = false);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task<int> GetUnreadCountAsync(int userId);
    }
}
