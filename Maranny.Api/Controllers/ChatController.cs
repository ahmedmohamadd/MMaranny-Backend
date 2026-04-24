using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        // Send message
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { error = "Message content is required" });
            }

            try
            {
                var message = await _chatService.SendMessageAsync(userId, request.ReceiverId, request.Content);

                return Ok(new
                {
                    messageId = message.MessageID,
                    content = message.Content,
                    sentAt = message.SentAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to send message", details = ex.Message });
            }
        }

        // Get conversation with specific user
        [HttpGet("conversation/{otherUserId}")]
        public async Task<IActionResult> GetConversation(int otherUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var messages = await _chatService.GetConversationAsync(userId, otherUserId, page, pageSize);

            var result = messages.Select(m => new
            {
                m.MessageID,
                m.SenderID,
                m.ReceiverID,
                m.Content,
                m.SentAt,
                m.IsRead,
                m.ReadAt,
                IsMine = m.SenderID == userId
            });

            return Ok(result);
        }

        // Get all conversations
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var conversations = await _chatService.GetUserConversationsAsync(userId);
            return Ok(conversations);
        }

        // Mark messages as read
        [HttpPut("conversation/{otherUserId}/read")]
        public async Task<IActionResult> MarkAsRead(int otherUserId)
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            await _chatService.MarkMessagesAsReadAsync(otherUserId, userId);
            return Ok(new { message = "Messages marked as read" });
        }

        // Get unread message count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount([FromQuery] int? fromUserId = null)
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var count = await _chatService.GetUnreadMessageCountAsync(userId, fromUserId);
            return Ok(new { unreadCount = count });
        }
    }

    // Request model
    public class SendMessageRequest
    {
        public int ReceiverId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}