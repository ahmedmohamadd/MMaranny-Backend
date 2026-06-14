using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.BookSession
{
    public interface IBookSessionUseCase
    {
        Task<Result<object>> ExecuteAsync(BookSessionCommand command);
    }
}
