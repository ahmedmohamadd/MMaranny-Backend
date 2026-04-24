using Google.Apis.Auth;
using Maranny.Application.DTOs.Auth;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Maranny.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IJwtService _jwtService;
        private readonly IEmailValidationService _emailValidationService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext,
            IJwtService jwtService,
            IEmailValidationService emailValidationService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _dbContext = dbContext;
            _jwtService = jwtService;
            _emailValidationService = emailValidationService;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task<(bool success, string message, object? data)> RegisterAsync(
            RegisterDto dto, string scheme, string host)
        {
            dto.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                ? null : dto.PhoneNumber.Trim();

            var emailValidation = await _emailValidationService.ValidateEmailDetailed(dto.Email);
            if (!emailValidation.isValid)
                return (false, emailValidation.reason, null);

            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return (false, "Email is already registered", null);

            var isCoach = dto.UserType.Equals("Coach", StringComparison.OrdinalIgnoreCase);
            if (isCoach && dto.IsCertified && string.IsNullOrWhiteSpace(dto.CertificateImageUrl))
                return (false, "Certificate image is required for certified coaches", null);

            var user = new ApplicationUser
            {
                Email = dto.Email,
                UserName = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                PrimaryUserType = isCoach ? UserType.Coach : UserType.Client,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return (false, string.Join(", ", result.Errors.Select(e => e.Description)), null);

            if (isCoach)
            {
                _dbContext.Coaches.Add(new Coach
                {
                    UserId = user.Id,
                    F_name = dto.FirstName,
                    L_name = dto.LastName,
                    VerificationStatus = VerificationStatus.Pending,
                    ExperienceYears = 0,
                    NationalIdImageUrl = dto.NationalIdImageUrl ?? string.Empty,
                    IsCertified = dto.IsCertified,
                    CertificateImageUrl = dto.CertificateImageUrl
                });
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Client");
                _dbContext.Clients.Add(new Client
                {
                    UserId = user.Id,
                    F_name = dto.FirstName,
                    L_name = dto.LastName,
                    Email = dto.Email
                });
            }

            await _dbContext.SaveChangesAsync();

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = $"{scheme}://{host}/api/auth/confirm-email" +
                                       $"?userId={user.Id}&token={Uri.EscapeDataString(token)}";
                await _emailService.SendEmailConfirmationAsync(user.Email!, dto.FirstName, confirmationLink);
            }
            catch { }

            var message = isCoach
                ? "Registration successful. Complete your coach profile, then confirm your email."
                : "Registration successful. Please check your email to confirm your account.";

            return (true, message, new
            {
                id = user.Id,
                email = user.Email,
                firstName = dto.FirstName,
                lastName = dto.LastName,
                userType = user.PrimaryUserType.ToString()
            });
        }

        public async Task<(bool success, int statusCode, string message, object? data)> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.Users
                .Include(u => u.Client).Include(u => u.Coach).Include(u => u.Admin)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null) return (false, 401, "Invalid email or password", null);
            if (user.IsBlocked) return (false, 403, "Your account has been blocked by an administrator.", null);
            if (!user.EmailConfirmed) return (false, 403, "Please confirm your email address before logging in.", null);

            if (await _userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                var minutesLeft = (lockoutEnd - DateTimeOffset.UtcNow)?.TotalMinutes ?? 0;
                return (false, 403, $"Account locked. Try again in {Math.Ceiling(minutesLeft)} minutes", null);
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid)
            {
                await _userManager.AccessFailedAsync(user);
                if (await _userManager.IsLockedOutAsync(user))
                    return (false, 403, "Account locked. Try again in 15 minutes", null);

                var failedAttempts = await _userManager.GetAccessFailedCountAsync(user);
                var attemptsRemaining = Math.Max(0, 5 - failedAttempts);
                return (false, 401, $"Invalid credentials. {attemptsRemaining} attempts remaining", null);
            }

            if (user.Coach != null)
            {
                var coachOnboardingState = await BuildCoachOnboardingStateAsync(user.Coach);
                if (!coachOnboardingState.IsComplete)
                    return (false, 403, "Please complete the Become a Coach flow before logging in.", null);
            }

            await _userManager.ResetAccessFailedCountAsync(user);
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtService.GenerateAccessToken(user, roles);
            var refreshToken = _jwtService.GenerateRefreshToken();

            _dbContext.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            return (true, 200, "Login successful", new
            {
                accessToken,
                refreshToken,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.Client?.F_name ?? user.Coach?.F_name ?? user.Admin?.F_name ?? "",
                    lastName = user.Client?.L_name ?? user.Coach?.L_name ?? user.Admin?.L_name ?? "",
                    userType = user.PrimaryUserType.ToString(),
                    roles
                }
            });
        }

        public async Task<(bool success, string message, object? data)> CompleteCoachOnboardingAsync(
            CompleteCoachOnboardingDto dto)
        {
            var user = await _userManager.Users
                .Include(u => u.Coach)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null || user.Coach == null || user.PrimaryUserType != UserType.Coach)
                return (false, "Coach account not found", null);

            if (user.IsBlocked)
                return (false, "AccountBlocked", null);

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid) return (false, "Invalid email or password", null);

            var distinctSportIds = dto.Sports.Select(s => s.SportID).Distinct().ToList();
            var existingSports = await _dbContext.Sports
                .Where(s => distinctSportIds.Contains(s.Id)).Select(s => s.Id).ToListAsync();
            var missingSportIds = distinctSportIds.Except(existingSports).ToList();
            if (missingSportIds.Count != 0)
                return (false, "One or more selected sports do not exist", new { sportIds = missingSportIds });

            ApplyCoachOnboarding(user.Coach, dto);

            var existingCoachSports = await _dbContext.CoachSports
                .Where(cs => cs.CoachID == user.Coach.CoachID).ToListAsync();
            _dbContext.CoachSports.RemoveRange(existingCoachSports);
            _dbContext.CoachSports.AddRange(dto.Sports.Select(s => new CoachSport
            {
                CoachID = user.Coach.CoachID,
                SportID = s.SportID,
                Description = s.Description?.Trim(),
                PricePerSession = dto.SessionPrice,
                ExperienceYears = dto.ExperienceYears
            }));

            var existingLocations = await _dbContext.CoachLocations
                .Where(cl => cl.CoachID == user.Coach.CoachID).ToListAsync();
            _dbContext.CoachLocations.RemoveRange(existingLocations);
            _dbContext.CoachLocations.Add(new CoachLocation
            {
                CoachID = user.Coach.CoachID,
                WorkingLocation = dto.City.Trim()
            });

            await _dbContext.SaveChangesAsync();

            return (true, user.EmailConfirmed
                ? "Coach profile completed. You can log in once your account is verified."
                : "Coach profile completed. Please confirm your email before logging in.",
                new { emailConfirmed = user.EmailConfirmed, verificationStatus = user.Coach.VerificationStatus.ToString() });
        }

        public async Task<(bool success, int statusCode, string message, object? data)> RefreshTokenAsync(
            RefreshTokenDto dto)
        {
            var userId = _jwtService.GetUserIdFromExpiredToken(dto.AccessToken);
            if (userId == null) return (false, 401, "Invalid access token", null);

            var refreshToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken && rt.UserId == userId);

            if (refreshToken == null || refreshToken.IsRevoked || refreshToken.ExpiresAt < DateTime.UtcNow)
                return (false, 401, "Invalid or expired refresh token", null);

            var user = await _userManager.Users
                .Include(u => u.Client).Include(u => u.Coach).Include(u => u.Admin)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.IsBlocked)
                return (false, 401, "User not found or blocked", null);

            var roles = await _userManager.GetRolesAsync(user);
            var newAccessToken = _jwtService.GenerateAccessToken(user, roles);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.ReplacedByToken = newRefreshToken;

            _dbContext.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            return (true, 200, "OK", new { accessToken = newAccessToken, refreshToken = newRefreshToken });
        }

        public async Task<(bool success, string message)> LogoutAsync(int userId, LogoutDto dto)
        {
            var token = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken && rt.UserId == userId);

            if (token != null)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }

            return (true, "Logged out successfully");
        }

        public async Task<(bool success, string message, object? data)> GetCurrentUserAsync(int userId)
        {
            var user = await _userManager.Users
                .Include(u => u.Client).Include(u => u.Coach).Include(u => u.Admin)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return (false, "User not found", null);

            var roles = await _userManager.GetRolesAsync(user);
            CoachOnboardingState? coachState = null;
            if (user.Coach != null)
                coachState = await BuildCoachOnboardingStateAsync(user.Coach);

            return (true, "OK", new
            {
                id = user.Id,
                email = user.Email,
                firstName = user.Client?.F_name ?? user.Coach?.F_name ?? user.Admin?.F_name ?? "",
                lastName = user.Client?.L_name ?? user.Coach?.L_name ?? user.Admin?.L_name ?? "",
                phoneNumber = user.PhoneNumber,
                userType = user.PrimaryUserType.ToString(),
                roles,
                emailConfirmed = user.EmailConfirmed,
                isBlocked = user.IsBlocked,
                profilePicture = user.Client?.URL ?? user.Coach?.URL,
                verificationStatus = user.Coach?.VerificationStatus.ToString(),
                coachSetupCompleted = coachState?.IsComplete
            });
        }

        public async Task<(bool success, string message)> ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return (true, "If the email exists, a reset code has been sent");

            var random = new Random();
            var resetCode = random.Next(100000, 999999).ToString();

            var existingTokens = await _dbContext.PasswordResetTokens
                .Where(prt => prt.UserId == user.Id && !prt.IsUsed).ToListAsync();
            foreach (var t in existingTokens) { t.IsUsed = true; t.UsedAt = DateTime.UtcNow; }

            _dbContext.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                Token = resetCode,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            try
            {
                var firstName = user.Client?.F_name ?? user.Coach?.F_name ?? user.Email;
                await _emailService.SendPasswordResetAsync(user.Email!, firstName!, resetCode);
            }
            catch { }

            return (true, "If the email exists, a reset code has been sent");
        }

        public async Task<(bool success, string message)> ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return (false, "Invalid email or token");

            var resetToken = await _dbContext.PasswordResetTokens
                .FirstOrDefaultAsync(prt => prt.UserId == user.Id && prt.Token == dto.Token
                    && !prt.IsUsed && prt.ExpiresAt > DateTime.UtcNow);
            if (resetToken == null) return (false, "Invalid or expired reset token");

            var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, identityToken, dto.NewPassword);
            if (!result.Succeeded)
                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

            resetToken.IsUsed = true;
            resetToken.UsedAt = DateTime.UtcNow;

            var refreshTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == user.Id && !rt.IsRevoked).ToListAsync();
            foreach (var rt in refreshTokens) { rt.IsRevoked = true; rt.RevokedAt = DateTime.UtcNow; }

            await _dbContext.SaveChangesAsync();
            return (true, "Password reset successfully");
        }

        public async Task<(bool success, string message)> ChangePasswordAsync(int userId, ChangePasswordDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return (false, "User not found");

            if (!await _userManager.CheckPasswordAsync(user, dto.CurrentPassword))
                return (false, "Current password is incorrect");

            if (dto.CurrentPassword == dto.NewPassword)
                return (false, "New password must be different from current password");

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

            var refreshTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked).ToListAsync();
            foreach (var token in refreshTokens) { token.IsRevoked = true; token.RevokedAt = DateTime.UtcNow; }
            await _dbContext.SaveChangesAsync();

            return (true, "Password changed successfully. Please login again.");
        }

        public async Task<(bool success, string message)> ConfirmEmailAsync(int userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return (false, "User not found");
            if (user.EmailConfirmed) return (true, "Email already confirmed");

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded) return (false, "Email confirmation failed");

            return (true, "Email confirmed successfully! You can now login.");
        }

        public async Task<(bool success, string message)> ResendConfirmationAsync(
            ResendConfirmationDto dto, string scheme, string host)
        {
            const string genericMessage = "If the email exists, a confirmation link has been sent";
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || user.EmailConfirmed) return (true, genericMessage);

            var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = $"{scheme}://{host}/api/auth/confirm-email" +
                                   $"?userId={user.Id}&token={Uri.EscapeDataString(confirmationToken)}";
            try
            {
                var firstName = user.Client?.F_name ?? user.Coach?.F_name ?? user.Email;
                await _emailService.SendEmailConfirmationAsync(user.Email!, firstName!, confirmationLink);
            }
            catch { return (false, "Failed to send confirmation email"); }

            return (true, genericMessage);
        }

        public async Task<(bool success, int statusCode, string message, object? data)> GoogleLoginAsync(
            GoogleLoginDto dto)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { _configuration["GoogleAuth:ClientId"]! }
                    });

                if (payload == null) return (false, 401, "Invalid Google token", null);

                var user = await _userManager.FindByEmailAsync(payload.Email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        Email = payload.Email,
                        UserName = payload.Email,
                        EmailConfirmed = true,
                        PrimaryUserType = dto.UserType == "Coach" ? UserType.Coach : UserType.Client,
                        CreatedAt = DateTime.UtcNow
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                        return (false, 400, string.Join(", ", createResult.Errors.Select(e => e.Description)), null);

                    if (dto.UserType == "Coach")
                    {
                        _dbContext.Coaches.Add(new Coach
                        {
                            UserId = user.Id,
                            F_name = payload.GivenName ?? payload.Email.Split('@')[0],
                            L_name = payload.FamilyName ?? "",
                            VerificationStatus = VerificationStatus.Pending,
                            ExperienceYears = 0
                        });
                    }
                    else
                    {
                        _dbContext.Clients.Add(new Client
                        {
                            UserId = user.Id,
                            F_name = payload.GivenName ?? payload.Email.Split('@')[0],
                            L_name = payload.FamilyName ?? "",
                            Email = payload.Email
                        });
                        await _userManager.AddToRoleAsync(user, "Client");
                    }
                    await _dbContext.SaveChangesAsync();
                }
                else if (user.IsBlocked)
                {
                    return (false, 403, "Your account has been blocked.", null);
                }

                var roles = await _userManager.GetRolesAsync(user);
                var accessToken = _jwtService.GenerateAccessToken(user, roles.ToList());
                var newRefreshToken = _jwtService.GenerateRefreshToken();

                _dbContext.RefreshTokens.Add(new RefreshToken
                {
                    UserId = user.Id,
                    Token = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                    CreatedAt = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();

                user = await _userManager.Users
                    .Include(u => u.Client).Include(u => u.Coach)
                    .FirstOrDefaultAsync(u => u.Id == user.Id);

                return (true, 200, "OK", new
                {
                    accessToken,
                    refreshToken = newRefreshToken,
                    user = new
                    {
                        id = user!.Id,
                        email = user.Email,
                        firstName = user.Client?.F_name ?? user.Coach?.F_name ?? payload.GivenName,
                        lastName = user.Client?.L_name ?? user.Coach?.L_name ?? payload.FamilyName,
                        userType = user.PrimaryUserType.ToString(),
                        roles
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, 400, $"Google authentication failed: {ex.Message}", null);
            }
        }

        private static void ApplyCoachOnboarding(Coach coach, CompleteCoachOnboardingDto dto)
        {
            var parts = dto.FullName.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            coach.F_name = parts[0];
            coach.L_name = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";
            coach.ID = dto.NationalId.Trim();
            coach.Bio = dto.Bio?.Trim();
            coach.ExperienceYears = dto.ExperienceYears;
            coach.CertificateUrl = dto.CertificateUrl?.Trim();
            coach.AvailabilityStatus = string.Join(",",
                dto.AvailableDays.Where(d => !string.IsNullOrWhiteSpace(d))
                    .Select(d => d.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private async Task<CoachOnboardingState> BuildCoachOnboardingStateAsync(Coach coach)
        {
            var sportRows = await _dbContext.CoachSports
                .Where(cs => cs.CoachID == coach.CoachID).ToListAsync();
            var hasLocation = await _dbContext.CoachLocations
                .AnyAsync(cl => cl.CoachID == coach.CoachID && !string.IsNullOrWhiteSpace(cl.WorkingLocation));

            return new CoachOnboardingState(
                !string.IsNullOrWhiteSpace(coach.F_name),
                !string.IsNullOrWhiteSpace(coach.ID),
                !string.IsNullOrWhiteSpace(coach.AvailabilityStatus),
                coach.ExperienceYears.HasValue,
                sportRows.Count > 0, hasLocation,
                sportRows.Any(cs => cs.PricePerSession > 0));
        }

        private sealed record CoachOnboardingState(
            bool HasFullName, bool HasNationalId, bool HasAvailableDays,
            bool HasExperienceYears, bool HasSportSelection, bool HasLocation, bool HasSessionPrice)
        {
            public bool IsComplete => HasFullName && HasNationalId && HasAvailableDays &&
                HasExperienceYears && HasSportSelection && HasLocation && HasSessionPrice;
        }
    }
}