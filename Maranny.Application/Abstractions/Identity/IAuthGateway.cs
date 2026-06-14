using Maranny.Application.DTOs.Auth;

namespace Maranny.Application.Abstractions.Identity
{
    public interface IAuthGateway
    {
        Task<(bool success, string message, object? data)> RegisterAsync(RegisterDto dto, string scheme, string host);
        Task<(bool success, int statusCode, string message, object? data)> LoginAsync(LoginDto dto);
        Task<(bool success, string message, object? data)> CompleteCoachOnboardingAsync(CompleteCoachOnboardingDto dto);
        Task<(bool success, int statusCode, string message, object? data)> RefreshTokenAsync(RefreshTokenDto dto);
        Task<(bool success, string message)> LogoutAsync(int userId, LogoutDto dto);
        Task<(bool success, string message, object? data)> GetCurrentUserAsync(int userId);
        Task<(bool success, string message)> ForgotPasswordAsync(ForgotPasswordDto dto);
        Task<(bool success, string message)> ResetPasswordAsync(ResetPasswordDto dto);
        Task<(bool success, string message)> ChangePasswordAsync(int userId, ChangePasswordDto dto);
        Task<(bool success, string message)> ConfirmEmailAsync(int userId, string token);
        Task<(bool success, string message)> ResendConfirmationAsync(ResendConfirmationDto dto, string scheme, string host);
        Task<(bool success, int statusCode, string message, object? data)> GoogleLoginAsync(GoogleLoginDto dto);
    }
}
