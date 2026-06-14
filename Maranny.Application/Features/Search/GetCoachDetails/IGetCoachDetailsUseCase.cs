using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Search.GetCoachDetails
{
    public interface IGetCoachDetailsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetCoachDetailsQuery query);
    }
}
