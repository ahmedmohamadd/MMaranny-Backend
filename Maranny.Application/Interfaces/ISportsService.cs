using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Sports;

namespace Maranny.Application.Interfaces
{
    public interface ISportsService
    {
        Task<IEnumerable<object>> GetAllAsync();
        Task<(bool success, string message, object? data)> CreateAsync(CreateSportDto dto);
    }
}