namespace Maranny.Core.Policies
{
    public static class RefundPolicy
    {
        public const decimal PlatformFeeRate = 0.10m;
        public const decimal ClientCancellationRefundRate = 0.90m;
        public const double ClientCancellationRefundWindowHours = 24;

        public static decimal CalculatePlatformFee(decimal amount)
        {
            return amount * PlatformFeeRate;
        }

        public static decimal CalculateClientCancellationRefund(
            decimal amount,
            double hoursUntilSession)
        {
            return hoursUntilSession >= ClientCancellationRefundWindowHours
                ? amount * ClientCancellationRefundRate
                : 0m;
        }
    }
}
