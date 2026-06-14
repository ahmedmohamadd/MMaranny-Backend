using Maranny.Application.DTOs.Sports;
using Maranny.Application.Features.Sports.CreateSport;
using Maranny.Application.Features.Sports.GetSports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maranny.Api.Controllers
{
    [ApiController]
    [Route("api/sports")]
    public class SportsController : ControllerBase
    {
        private readonly IGetSportsUseCase _getSportsUseCase;
        private readonly ICreateSportUseCase _createSportUseCase;

        public SportsController(
            IGetSportsUseCase getSportsUseCase,
            ICreateSportUseCase createSportUseCase)
        {
            _getSportsUseCase = getSportsUseCase;
            _createSportUseCase = createSportUseCase;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _getSportsUseCase.ExecuteAsync();
            return Ok(result.Value);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateSportDto dto)
        {
            var result = await _createSportUseCase.ExecuteAsync(new CreateSportCommand(dto));
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(result.Value);
        }
    }
}
