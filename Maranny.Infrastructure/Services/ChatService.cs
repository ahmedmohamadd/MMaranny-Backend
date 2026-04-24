using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Entities;
using Maranny.Application.Interfaces;
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

        public async Task<ChatMessage> SendMessageAsync(int senderId, int receiverId, string content)
        {
            // Create message
            var message = new ChatMessage
            {
                SenderID = senderId,
                ReceiverID = receiverId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                MessageType = "text"
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
            // Get all users that current user has chatted with
            var conversations = await _dbContext.ChatMessages
                .Where(m => m.SenderID == userId || m.ReceiverID == userId)
                .GroupBy(m => m.SenderID == userId ? m.ReceiverID : m.SenderID)
                .Select(g => new
                {
                    OtherUserId = g.Key,
                    LastMessage = g.OrderByDescending(m => m.SentAt).FirstOrDefault(),
                    UnreadCount = g.Count(m => m.ReceiverID == userId && !m.IsRead)
                })
                .ToListAsync();

            // Get user details for each conversation
            var result = new List<object>();
            foreach (var conv in conversations)
            {
                var otherUser = await _dbContext.Users
                    .Include(u => u.Client)
                    .Include(u => u.Coach)
                    .FirstOrDefaultAsync(u => u.Id == conv.OtherUserId);

                if (otherUser != null && conv.LastMessage != null)
                {
                    result.Add(new
                    {
                        UserId = conv.OtherUserId,
                        Name = (otherUser.Client != null
    ? otherUser.Client.F_name + " " + otherUser.Client.L_name
    : otherUser.Coach != null
        ? otherUser.Coach.F_name + " " + otherUser.Coach.L_name
        : otherUser.Email),
                        LastMessage = conv.LastMessage.Content,
                        LastMessageTime = conv.LastMessage.SentAt,
                        UnreadCount = conv.UnreadCount,
                        IsOnline = ChatHub.IsUserOnline(conv.OtherUserId)
                    });
                }
            }

            return result.OrderByDescending(c => ((dynamic)c).LastMessageTime).ToList();
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
    }
}