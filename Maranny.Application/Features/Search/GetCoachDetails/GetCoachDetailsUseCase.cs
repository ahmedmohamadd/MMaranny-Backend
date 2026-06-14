using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Core.Entities;

namespace Maranny.Application.Features.Search.GetCoachDetails
{
    public sealed class GetCoachDetailsUseCase : IGetCoachDetailsUseCase
    {
        private readonly ISearchReadRepository _search;
        private readonly IBookingRepository _bookings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;

        public GetCoachDetailsUseCase(
            ISearchReadRepository search,
            IBookingRepository bookings,
            IUnitOfWork unitOfWork,
            IClock clock)
        {
            _search = search;
            _bookings = bookings;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<Result<object>> ExecuteAsync(GetCoachDetailsQuery query)
        {
            if (query.UserId.HasValue)
            {
                await _bookings.AddUserInteractionAsync(new UserInteraction
                {
                    UserId = query.UserId.Value,
                    CoachId = query.CoachId,
                    Type = "View",
                    Timestamp = _clock.UtcNow,
                    Context = "Viewed coach profile"
                });

                await _unitOfWork.SaveChangesAsync();
            }

            var data = await _search.GetCoachDetailsAsync(query.CoachId);

            return data == null
                ? Result<object>.Failure(new Error("Coach.NotFound", "Coach not found", ErrorType.NotFound))
                : Result<object>.Success(data);
        }
    }
}
