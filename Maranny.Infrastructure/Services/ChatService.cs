using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Entities;
using Maranny.Core.Interfaces;
using Maranny.Infrastructure.Data;
using Maranny.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatService(
            ApplicationDbContext dbContext,
            IHubContext<ChatHub> hubContext)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
        }

        public async Task<ChatMessage> SendMessageAsync(
            int senderId,
            int receiverId,
            string content,
            string messageType = "text",
            string? attachmentUrl = null,
            double? latitude = null,
            double? longitude = null)
        {
            // Create message
            var message = new ChatMessage
            {
                SenderID = senderId,
                ReceiverID = receiverId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                MessageType = string.IsNullOrWhiteSpace(messageType) ? "text" : messageType.Trim(),
                AttachmentUrl = attachmentUrl,
                Latitude = latitude,
                Longitude = longitude
            };

            _dbContext.ChatMessages.Add(message);
            await _dbContext.SaveChangesAsync();

            // Reload with sender/receiver info
            message = await _dbContext.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .FirstAsync(m => m.MessageID == message.MessageID);

            // Send real-time notification via SignalR
            var messageData = new
            {
                message.MessageID,
                message.SenderID,
                message.ReceiverID,
                message.Content,
                message.SentAt,
                message.IsRead,
                message.MessageType,
                message.AttachmentUrl,
                message.Latitude,
                message.Longitude,
                message.Reaction,
                SenderName = message.Sender.Email
            };

            await ChatHub.SendMessageToUser(_hubContext, receiverId, messageData);

            return message;
        }

        public async Task<List<ChatMessage>> GetConversationAsync(int userId1, int userId2, int page = 1, int pageSize = 50)
        {
            var messages = await _dbContext.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => (m.SenderID == userId1 && m.ReceiverID == userId2) ||
                           (m.SenderID == userId2 && m.ReceiverID == userId1))
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return messages.OrderBy(m => m.SentAt).ToList();
        }

        public async Task<List<object>> GetUserConversationsAsync(int userId)
        {
            var messages = await _dbContext.ChatMessages
                .Where(m => m.SenderID == userId || m.ReceiverID == userId)
                .OrderByDescending(m => m.SentAt)
                .AsNoTracking()
                .ToListAsync();

            if (messages.Count == 0)
            {
                return new List<object>();
            }

            var otherUserIds = messages
                .Select(m => m.SenderID == userId ? m.ReceiverID : m.SenderID)
                .Distinct()
                .ToList();

            var users = await _dbContext.Users
                .Include(u => u.Client)
                .Include(u => u.Coach)
                .Where(u => otherUserIds.Contains(u.Id))
                .AsNoTracking()
                .ToDictionaryAsync(u => u.Id);

            var result = messages
                .GroupBy(m => m.SenderID == userId ? m.ReceiverID : m.SenderID)
                .Select(group =>
                {
                    var otherUserId = group.Key;
                    var lastMessage = group.OrderByDescending(m => m.SentAt).First();
                    users.TryGetValue(otherUserId, out var otherUser);

                    return new
                    {
                        UserId = otherUserId,
                        Name = ResolveDisplayName(otherUser),
                        ImageUrl = otherUser?.Client?.URL ?? otherUser?.Coach?.URL,
                        LastMessage = ResolveLastMessagePreview(lastMessage),
                        LastMessageTime = lastMessage.SentAt,
                        UnreadCount = group.Count(m => m.ReceiverID == userId && !m.IsRead),
                        IsOnline = ChatHub.IsUserOnline(otherUserId)
                    };
                })
                .OrderByDescending(c => c.LastMessageTime)
                .Cast<object>()
                .ToList();

            return result;
        }

        private static string ResolveDisplayName(ApplicationUser? user)
        {
            if (user?.Client != null)
            {
                var name = $"{user.Client.F_name} {user.Client.L_name}".Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }

            if (user?.Coach != null)
            {
                var name = $"{user.Coach.F_name} {user.Coach.L_name}".Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }

            return user?.Email ?? "User";
        }

        private static string ResolveLastMessagePreview(ChatMessage message)
        {
            var messageType = message.MessageType?.Trim().ToLowerInvariant();
            return messageType switch
            {
                "image" => "Photo attachment",
                "location" => "Location shared",
                _ => message.Content
            };
        }

        public async Task MarkMessagesAsReadAsync(int senderId, int receiverId)
        {
            var unreadMessages = await _dbContext.ChatMessages
                .Where(m => m.SenderID == senderId && m.ReceiverID == receiverId && !m.IsRead)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task<int> GetUnreadMessageCountAsync(int userId, int? fromUserId = null)
        {
            var query = _dbContext.ChatMessages
                .Where(m => m.ReceiverID == userId && !m.IsRead);

            if (fromUserId.HasValue)
            {
                query = query.Where(m => m.SenderID == fromUserId.Value);
            }

            return await query.CountAsync();
        }

        public async Task<ChatMessage?> SetMessageReactionAsync(int userId, int messageId, string? reaction)
        {
            var message = await _dbContext.ChatMessages
                .FirstOrDefaultAsync(m => m.MessageID == messageId &&
                    (m.SenderID == userId || m.ReceiverID == userId));

            if (message == null)
            {
                return null;
            }

            message.Reaction = string.IsNullOrWhiteSpace(reaction)
                ? null
                : reaction.Trim();

            await _dbContext.SaveChangesAsync();

            var reactionData = new
            {
                message.MessageID,
                message.SenderID,
                message.ReceiverID,
                message.Reaction
            };

            await ChatHub.SendMessageToUser(_hubContext, message.SenderID, reactionData);
            await ChatHub.SendMessageToUser(_hubContext, message.ReceiverID, reactionData);

            return message;
        }
    }
}
