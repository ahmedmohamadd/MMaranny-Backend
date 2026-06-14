using Maranny.Application.Abstractions.Messaging;
using Maranny.Core.Entities;

namespace Maranny.Application.Features.Chat
{
    public class ChatUseCases : IChatUseCases
    {
        private readonly IChatGateway _chatGateway;

        public ChatUseCases(IChatGateway chatGateway)
        {
            _chatGateway = chatGateway;
        }

        public Task<ChatMessage> SendMessageAsync(int senderId, int receiverId, string content) =>
            _chatGateway.SendMessageAsync(senderId, receiverId, content);

        public Task<List<ChatMessage>> GetConversationAsync(int userId1, int userId2, int page = 1, int pageSize = 50) =>
            _chatGateway.GetConversationAsync(userId1, userId2, page, pageSize);

        public Task<List<object>> GetUserConversationsAsync(int userId) =>
            _chatGateway.GetUserConversationsAsync(userId);

        public Task MarkMessagesAsReadAsync(int senderId, int receiverId) =>
            _chatGateway.MarkMessagesAsReadAsync(senderId, receiverId);

        public Task<int> GetUnreadMessageCountAsync(int userId, int? fromUserId = null) =>
            _chatGateway.GetUnreadMessageCountAsync(userId, fromUserId);
    }
}
