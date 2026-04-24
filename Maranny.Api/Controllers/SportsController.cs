using Maranny.Application.DTOs.Sports;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maranny.Api.Controllers
{
    [ApiController]
    [Route("api/sports")]
    public class SportsController : ControllerBase
    {
        private readonly ISportsService _sportsService;

        public SportsController(ISportsService sportsService)
        {
            _sportsService = sportsService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var sports = await _sportsService.GetAllAsync();
            return Ok(sports);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateSportDto dto)
        {
            var (success, message, data) = await _sportsService.CreateAsync(dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(data);
        }
    }
}