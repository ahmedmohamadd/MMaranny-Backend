using Maranny.Application.Abstractions.Identity;
using Maranny.Application.DTOs.Auth;

namespace Maranny.Application.Features.Auth
{
    public class AuthUseCases : IAuthUseCases
    {
        private readonly IAuthGateway _authGateway;

        public AuthUseCases(IAuthGateway authGateway)
        {
            _authGateway = authGateway;
        }

        public Task<(bool success, string message, object? data)> RegisterAsync(RegisterDto dto, string scheme, string host) =>
            _authGateway.RegisterAsync(dto, scheme, host);

        public Task<(bool success, int statusCode, string message, object? data)> LoginAsync(LoginDto dto) =>
            _authGateway.LoginAsync(dto);

        public Task<(bool success, string message, object? data)> CompleteCoachOnboardingAsync(CompleteCoachOnboardingDto dto) =>
            _authGateway.CompleteCoachOnboardingAsync(dto);

        public Task<(bool success, int statusCode, string message, object? data)> RefreshTokenAsync(RefreshTokenDto dto) =>
            _authGateway.RefreshTokenAsync(dto);

        public Task<(bool success, string message)> LogoutAsync(int userId, LogoutDto dto) =>
            _authGateway.LogoutAsync(userId, dto);

        public Task<(bool success, string message, object? data)> GetCurrentUserAsync(int userId) =>
            _authGateway.GetCurrentUserAsync(userId);

        public Task<(bool success, string message)> ForgotPasswordAsync(ForgotPasswordDto dto) =>
            _authGateway.ForgotPasswordAsync(dto);

        public Task<(bool success, string message)> ResetPasswordAsync(ResetPasswordDto dto) =>
            _authGateway.ResetPasswordAsync(dto);

        public Task<(bool success, string message)> ChangePasswordAsync(int userId, ChangePasswordDto dto) =>
            _authGateway.ChangePasswordAsync(userId, dto);

        public Task<(bool success, string message)> ConfirmEmailAsync(int userId, string token) =>
            _authGateway.ConfirmEmailAsync(userId, token);

        public Task<(bool success, string message)> ResendConfirmationAsync(ResendConfirmationDto dto, string scheme, string host) =>
            _authGateway.ResendConfirmationAsync(dto, scheme, host);

        public Task<(bool success, int statusCode, string message, object? data)> GoogleLoginAsync(GoogleLoginDto dto) =>
            _authGateway.GoogleLoginAsync(dto);
    }
}
