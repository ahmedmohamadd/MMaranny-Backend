using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Sports;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Services
{
    public class SportsService : ISportsService
    {
        private readonly ApplicationDbContext _dbContext;

        public SportsService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<object>> GetAllAsync()
        {
            return await _dbContext.Sports
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync<object>();
        }

        public async Task<(bool success, string message, object? data)> CreateAsync(CreateSportDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return (false, "Sport name is required", null);

            var sport = new Sport { Name = dto.Name };
            _dbContext.Sports.Add(sport);
            await _dbContext.SaveChangesAsync();

            return (true, "Sport created successfully", new { sport.Id, sport.Name });
        }
    }
}