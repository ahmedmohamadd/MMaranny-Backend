using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Core.Entities;

namespace Maranny.Application.Features.Sports.CreateSport
{
    public sealed class CreateSportUseCase : ICreateSportUseCase
    {
        private readonly ISportRepository _sports;
        private readonly IUnitOfWork _unitOfWork;

        public CreateSportUseCase(ISportRepository sports, IUnitOfWork unitOfWork)
        {
            _sports = sports;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<object>> ExecuteAsync(CreateSportCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.Sport.Name))
            {
                return Result<object>.Failure(new Error(
                    "Sport.NameRequired",
                    "Sport name is required",
                    ErrorType.Validation));
            }

            var sport = new Sport { Name = command.Sport.Name };
            await _sports.AddAsync(sport);
            await _unitOfWork.SaveChangesAsync();

            return Result<object>.Success(new { sport.Id, sport.Name });
        }
    }
}
