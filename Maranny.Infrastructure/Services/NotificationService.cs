using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Application.Interfaces;
using Maranny.Infrastructure.Data;
using Maranny.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(
            ApplicationDbContext dbContext,
            IHubContext<NotificationHub> hubContext)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
        }

        public async Task SendNotificationAsync(int userId, string title, string message, NotificationType type)
        {
            // Create notification in database
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            // Create user-notification relationship
            var clientNotification = new ClientNotification
            {
                ClientID = userId, // Note: This assumes userId maps to ClientID
                NotificationID = notification.NotificationID
            };
            _dbContext.ClientNotifications.Add(clientNotification);
            await _dbContext.SaveChangesAsync();

            // Send real-time notification via SignalR
            var notificationData = new
            {
                notification.NotificationID,
                notification.Title,
                notification.Message,
                Type = notification.Type.ToString(),
                notification.CreatedAt,
                notification.IsRead
            };

            await NotificationHub.SendNotificationToUser(_hubContext, userId, notificationData);
        }

        public async Task SendNotificationToMultipleUsersAsync(List<int> userIds, string title, string message, NotificationType type)
        {
            // Create notification in database
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            // Create user-notification relationships
            foreach (var userId in userIds)
            {
                var clientNotification = new ClientNotification
                {
                    ClientID = userId,
                    NotificationID = notification.NotificationID
                };
                _dbContext.ClientNotifications.Add(clientNotification);
            }
            await _dbContext.SaveChangesAsync();

            // Send real-time notification via SignalR
            var notificationData = new
            {
                notification.NotificationID,
                notification.Title,
                notification.Message,
                Type = notification.Type.ToString(),
                notification.CreatedAt,
                notification.IsRead
            };

            await NotificationHub.SendNotificationToUsers(_hubContext, userIds, notificationData);
        }

        public async Task<List<object>> GetUserNotificationsAsync(int userId, bool unreadOnly = false)
        {
            var query = _dbContext.ClientNotifications
                .Include(cn => cn.Notification)
                .Where(cn => cn.ClientID == userId);

            if (unreadOnly)
            {
                query = query.Where(cn => !cn.Notification.IsRead);
            }

            var notifications = await query
                .OrderByDescending(cn => cn.Notification.CreatedAt)
                .Select(cn => new
                {
                    cn.Notification.NotificationID,
                    cn.Notification.Title,
                    cn.Notification.Message,
                    Type = cn.Notification.Type.ToString(),
                    cn.Notification.IsRead,
                    cn.Notification.CreatedAt
                })
                .ToListAsync();

            return notifications.Cast<object>().ToList();
        }

        public async Task MarkAsReadAsync(int notificationId, int userId)
        {
            var clientNotification = await _dbContext.ClientNotifications
                .Include(cn => cn.Notification)
                .FirstOrDefaultAsync(cn => cn.NotificationID == notificationId && cn.ClientID == userId);

            if (clientNotification != null)
            {
                clientNotification.Notification.IsRead = true;
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _dbContext.ClientNotifications
                .Include(cn => cn.Notification)
                .Where(cn => cn.ClientID == userId && !cn.Notification.IsRead)
                .CountAsync();
        }
    }
}