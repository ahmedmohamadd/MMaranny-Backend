using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Entities;

namespace Maranny.Application.Interfaces
{
    public interface IChatService
    {
        Task<ChatMessage> SendMessageAsync(int senderId, int receiverId, string content);
        Task<List<ChatMessage>> GetConversationAsync(int userId1, int userId2, int page = 1, int pageSize = 50);
        Task<List<object>> GetUserConversationsAsync(int userId);
        Task MarkMessagesAsReadAsync(int senderId, int receiverId);
        Task<int> GetUnreadMessageCountAsync(int userId, int? fromUserId = null);
    }
}