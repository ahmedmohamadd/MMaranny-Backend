using Maranny.Core.Enums;

namespace Maranny.Core.Policies
{
    public static class BookingPolicy
    {
        public static bool CanBookSession(
            SessionStatus sessionStatus,
            DateTime sessionStartUtc,
            DateTime nowUtc,
            int currentBookings,
            int? maxParticipants)
        {
            var hasCapacity = !maxParticipants.HasValue || currentBookings < maxParticipants.Value;

            return sessionStatus == SessionStatus.Scheduled &&
                   sessionStartUtc > nowUtc &&
                   hasCapacity;
        }

        public static bool HasOverlappingTimeRange(
            TimeSpan candidateStart,
            TimeSpan candidateEnd,
            TimeSpan existingStart,
            TimeSpan existingEnd)
        {
            return (candidateStart >= existingStart && candidateStart < existingEnd) ||
                   (candidateEnd > existingStart && candidateEnd <= existingEnd) ||
                   (candidateStart <= existingStart && candidateEnd >= existingEnd);
        }

        public static bool CanCancel(BookingStatus status, DateTime sessionStartUtc, DateTime nowUtc)
        {
            return status != BookingStatus.Cancelled &&
                   status != BookingStatus.Completed &&
                   sessionStartUtc > nowUtc;
        }

        public static bool CanCoachCancelSession(
            SessionStatus status,
            DateTime sessionStartUtc,
            DateTime nowUtc)
        {
            return status != SessionStatus.Cancelled &&
                   status != SessionStatus.Completed &&
                   sessionStartUtc > nowUtc;
        }
    }
}
