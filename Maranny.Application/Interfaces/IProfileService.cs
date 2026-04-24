using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Profile;

namespace Maranny.Application.Interfaces
{
    public interface IProfileService
    {
        Task<bool> UpdateProfileAsync(UpdateProfileDto dto);
        Task<bool> UpdatePreferencesAsync(UpdatePreferencesDto dto);
        Task<bool> CoachSetupAsync(UpdateCoachSetupDto dto);
    }
}