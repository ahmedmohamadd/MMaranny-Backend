using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Maranny.Application.Interfaces
{
    public interface IRealtimeService
    {
        Task SendNotificationAsync(string userId, string message);
        Task SendMessageAsync(string userId, string content);
    }
}