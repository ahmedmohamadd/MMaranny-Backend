using Maranny.Application.Abstractions.Profiles;
using Maranny.Application.DTOs.Profile;

namespace Maranny.Application.Features.Users
{
    public class UserUseCases : IUserUseCases
    {
        private readonly IUserProfileGateway _userProfileGateway;

        public UserUseCases(IUserProfileGateway userProfileGateway)
        {
            _userProfileGateway = userProfileGateway;
        }

        public Task<(bool success, string message)> UpdateProfileAsync(int userId, UpdateProfileDto dto) =>
            _userProfileGateway.UpdateProfileAsync(userId, dto);

        public Task<(bool success, string message, object? data)> UpdatePreferencesAsync(int userId, UpdatePreferencesDto dto) =>
            _userProfileGateway.UpdatePreferencesAsync(userId, dto);

        public Task<(bool success, object? data)> GetCoachSetupAsync(int userId) =>
            _userProfileGateway.GetCoachSetupAsync(userId);

        public Task<(bool success, string message)> UpdateCoachSetupAsync(int userId, UpdateCoachSetupDto dto) =>
            _userProfileGateway.UpdateCoachSetupAsync(userId, dto);

        public Task<(bool success, string message, object? data)> UploadProfileImageAsync(
            int userId,
            Stream fileStream,
            string fileName,
            long fileSize) =>
            _userProfileGateway.UploadProfileImageAsync(userId, fileStream, fileName, fileSize);
    }
}
