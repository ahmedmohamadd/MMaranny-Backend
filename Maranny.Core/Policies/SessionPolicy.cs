using Maranny.Core.Enums;

namespace Maranny.Core.Policies
{
    public static class SessionPolicy
    {
        public static bool CanCreateSession(
            VerificationStatus verificationStatus,
            DateTime sessionDate,
            DateTime todayUtc,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            return IsVerifiedCoach(verificationStatus) &&
                   sessionDate.Date >= todayUtc.Date &&
                   endTime > startTime;
        }

        public static bool IsVerifiedCoach(VerificationStatus verificationStatus)
        {
            return verificationStatus == VerificationStatus.Verified ||
                   verificationStatus == VerificationStatus.Approved;
        }
    }
}
