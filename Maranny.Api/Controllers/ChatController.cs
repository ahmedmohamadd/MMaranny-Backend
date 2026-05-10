using Maranny.Core.Interfaces;
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
        private readonly IWebHostEnvironment _environment;

        public ChatController(IChatService chatService, IWebHostEnvironment environment)
        {
            _chatService = chatService;
            _environment = environment;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
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
                var message = await _chatService.SendMessageAsync(
                    userId,
                    request.ReceiverId,
                    request.Content,
                    request.MessageType,
                    request.AttachmentUrl,
                    request.Latitude,
                    request.Longitude);

                return Ok(new
                {
                    messageId = message.MessageID,
                    content = message.Content,
                    message.MessageType,
                    message.AttachmentUrl,
                    message.Latitude,
                    message.Longitude,
                    message.Reaction,
                    sentAt = message.SentAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to send message", details = ex.Message });
            }
        }

        [HttpPost("send-attachment")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SendAttachment([FromForm] SendAttachmentRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var messageType = request.MessageType?.Trim().ToLowerInvariant();
            if (messageType != "image" && messageType != "location")
            {
                return BadRequest(new { error = "Attachment type must be image or location" });
            }

            string? attachmentUrl = null;
            var content = request.Content?.Trim();

            if (messageType == "image")
            {
                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest(new { error = "Image file is required" });
                }

                var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest(new { error = "Only JPG, PNG, WEBP, or GIF images are allowed" });
                }

                if (request.File.Length > 10 * 1024 * 1024)
                {
                    return BadRequest(new { error = "Image must be 10 MB or smaller" });
                }

                var webRoot = _environment.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRoot))
                {
                    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                var uploadDirectory = Path.Combine(webRoot, "uploads", "chat");
                Directory.CreateDirectory(uploadDirectory);

                var fileName = $"{userId}_{Guid.NewGuid():N}{extension}";
                var fullPath = Path.Combine(uploadDirectory, fileName);

                await using (var stream = System.IO.File.Create(fullPath))
                {
                    await request.File.CopyToAsync(stream);
                }

                attachmentUrl = $"/uploads/chat/{fileName}";
                content = string.IsNullOrWhiteSpace(content) ? "Photo attachment" : content;
            }
            else
            {
                if (!request.Latitude.HasValue || !request.Longitude.HasValue)
                {
                    return BadRequest(new { error = "Latitude and longitude are required for location messages" });
                }

                content = string.IsNullOrWhiteSpace(content) ? "Location shared" : content;
            }

            try
            {
                var message = await _chatService.SendMessageAsync(
                    userId,
                    request.ReceiverId,
                    content ?? string.Empty,
                    messageType,
                    attachmentUrl,
                    request.Latitude,
                    request.Longitude);

                return Ok(new
                {
                    messageId = message.MessageID,
                    content = message.Content,
                    message.MessageType,
                    message.AttachmentUrl,
                    message.Latitude,
                    message.Longitude,
                    message.Reaction,
                    sentAt = message.SentAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to send attachment", details = ex.Message });
            }
        }

        [HttpGet("conversation/{otherUserId}")]
        public async Task<IActionResult> GetConversation(int otherUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
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
                m.MessageType,
                m.AttachmentUrl,
                m.Latitude,
                m.Longitude,
                m.Reaction,
                IsMine = m.SenderID == userId
            });

            return Ok(result);
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var conversations = await _chatService.GetUserConversationsAsync(userId);
            return Ok(conversations);
        }

        [HttpPut("conversation/{otherUserId}/read")]
        public async Task<IActionResult> MarkAsRead(int otherUserId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            await _chatService.MarkMessagesAsReadAsync(otherUserId, userId);
            return Ok(new { message = "Messages marked as read" });
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount([FromQuery] int? fromUserId = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var count = await _chatService.GetUnreadMessageCountAsync(userId, fromUserId);
            return Ok(new { unreadCount = count });
        }

        [HttpPost("messages/{messageId}/reaction")]
        [HttpPut("messages/{messageId}/reaction")]
        public async Task<IActionResult> SetReaction(int messageId, [FromBody] SetReactionRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            if (!string.IsNullOrWhiteSpace(request.Reaction) && request.Reaction.Length > 20)
            {
                return BadRequest(new { error = "Reaction is too long" });
            }

            var message = await _chatService.SetMessageReactionAsync(
                userId,
                messageId,
                request.Reaction);

            if (message == null)
            {
                return NotFound(new { error = "Message not found" });
            }

            return Ok(new
            {
                message.MessageID,
                message.Reaction
            });
        }
    }

    public class SendMessageRequest
    {
        public int ReceiverId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "text";
        public string? AttachmentUrl { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class SendAttachmentRequest
    {
        public int ReceiverId { get; set; }
        public string? Content { get; set; }
        public string? MessageType { get; set; }
        public IFormFile? File { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class SetReactionRequest
    {
        public string? Reaction { get; set; }
    }
}
