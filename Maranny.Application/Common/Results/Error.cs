namespace Maranny.Application.Common.Results
{
    public sealed record Error(string Code, string Message, ErrorType Type);
}
