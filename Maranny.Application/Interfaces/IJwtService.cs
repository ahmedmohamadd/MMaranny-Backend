using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Entities;
using System.Security.Claims;

namespace Maranny.Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateAccessToken(ApplicationUser user, IList<string> roles);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token);
        int? GetUserIdFromExpiredToken(string token);
    }
}