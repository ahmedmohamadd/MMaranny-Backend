using Maranny.Application.Interfaces;
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
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
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

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly);
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

            var count = await _notificationService.GetUnreadCountAsync(userId);
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

            await _notificationService.MarkAsReadAsync(notificationId, userId);
            return Ok(new { message = "Notification marked as read" });
        }
    }
}