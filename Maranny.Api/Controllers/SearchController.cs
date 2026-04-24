using Maranny.Application.DTOs.Search;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet("coaches")]
        public async Task<IActionResult> SearchCoaches([FromQuery] CoachSearchDto dto)
        {
            var result = await _searchService.SearchCoachesAsync(dto);
            return Ok(result);
        }

        [HttpGet("coaches/{coachId}")]
        public async Task<IActionResult> GetCoachDetails(int coachId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? userId = int.TryParse(userIdClaim, out int id) ? id : null;

            var (success, data) = await _searchService.GetCoachDetailsAsync(coachId, userId);
            if (!success) return NotFound(new { error = "Coach not found" });
            return Ok(data);
        }
    }
}