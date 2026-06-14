namespace Maranny.Application.Abstractions.Common
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
