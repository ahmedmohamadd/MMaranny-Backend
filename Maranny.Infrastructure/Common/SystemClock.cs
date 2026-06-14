using Maranny.Application.Abstractions.Common;

namespace Maranny.Infrastructure.Common
{
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
