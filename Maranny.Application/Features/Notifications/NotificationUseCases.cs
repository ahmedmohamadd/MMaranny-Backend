using Maranny.Application.Abstractions.Notifications;
using Maranny.Core.Enums;

namespace Maranny.Application.Features.Notifications
{
    public class NotificationUseCases : INotificationUseCases
    {
        private readonly INotificationGateway _notificationGateway;

        public NotificationUseCases(INotificationGateway notificationGateway)
        {
            _notificationGateway = notificationGateway;
        }

        public Task SendNotificationAsync(int userId, string title, string message, NotificationType type) =>
            _notificationGateway.SendNotificationAsync(userId, title, message, type);

        public Task SendNotificationToMultipleUsersAsync(List<int> userIds, string title, string message, NotificationType type) =>
            _notificationGateway.SendNotificationToMultipleUsersAsync(userIds, title, message, type);

        public Task<List<object>> GetUserNotificationsAsync(int userId, bool unreadOnly = false) =>
            _notificationGateway.GetUserNotificationsAsync(userId, unreadOnly);

        public Task MarkAsReadAsync(int notificationId, int userId) =>
            _notificationGateway.MarkAsReadAsync(notificationId, userId);

        public Task<int> GetUnreadCountAsync(int userId) =>
            _notificationGateway.GetUnreadCountAsync(userId);
    }
}
