using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Search;

namespace Maranny.Application.Interfaces
{
    public interface ISearchService
    {
        Task<object> SearchCoachesAsync(CoachSearchDto dto);
        Task<(bool success, object? data)> GetCoachDetailsAsync(int coachId, int? userId);
    }
}