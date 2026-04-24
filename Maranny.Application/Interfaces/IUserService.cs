using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Application.DTOs.Profile;

namespace Maranny.Application.Interfaces
{
    public interface IUserService
    {
        Task<(bool success, string message)> UpdateProfileAsync(int userId, UpdateProfileDto dto);
        Task<(bool success, string message, object? data)> UpdatePreferencesAsync(int userId, UpdatePreferencesDto dto);
        Task<(bool success, object? data)> GetCoachSetupAsync(int userId);
        Task<(bool success, string message)> UpdateCoachSetupAsync(int userId, UpdateCoachSetupDto dto);
        Task<(bool success, string message, object? data)> UploadProfileImageAsync(int userId, Stream fileStream, string fileName, long fileSize);
    }
}