using Maranny.Application.Features.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationUseCases _notificationUseCases;

        public NotificationsController(INotificationUseCases notificationUseCases)
        {
            _notificationUseCases = notificationUseCases;
        }

        // Get my notifications
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool unreadOnly = false)
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var notifications = await _notificationUseCases.GetUserNotificationsAsync(userId, unreadOnly);
            return Ok(notifications);
        }

        // Get unread count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var count = await _notificationUseCases.GetUnreadCountAsync(userId);
            return Ok(new { unreadCount = count });
        }

        // Mark notification as read
        [HttpPut("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            await _notificationUseCases.MarkAsReadAsync(notificationId, userId);
            return Ok(new { message = "Notification marked as read" });
        }
    }
}