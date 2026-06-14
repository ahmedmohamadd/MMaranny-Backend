using Maranny.Application.DTOs.Search;
using Maranny.Application.Features.Search.GetCoachDetails;
using Maranny.Application.Features.Search.SearchCoaches;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchCoachesUseCase _searchCoachesUseCase;
        private readonly IGetCoachDetailsUseCase _getCoachDetailsUseCase;

        public SearchController(
            ISearchCoachesUseCase searchCoachesUseCase,
            IGetCoachDetailsUseCase getCoachDetailsUseCase)
        {
            _searchCoachesUseCase = searchCoachesUseCase;
            _getCoachDetailsUseCase = getCoachDetailsUseCase;
        }

        [HttpGet("coaches")]
        public async Task<IActionResult> SearchCoaches([FromQuery] CoachSearchDto dto)
        {
            var result = await _searchCoachesUseCase.ExecuteAsync(new SearchCoachesQuery(dto));
            return Ok(result.Value);
        }

        [HttpGet("coaches/{coachId}")]
        public async Task<IActionResult> GetCoachDetails(int coachId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? userId = int.TryParse(userIdClaim, out int id) ? id : null;

            var result = await _getCoachDetailsUseCase.ExecuteAsync(new GetCoachDetailsQuery(coachId, userId));
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(result.Value);
        }
    }
}
