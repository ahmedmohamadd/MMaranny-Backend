using Maranny.Application.DTOs.Auth;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Core.Interfaces;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Google.Apis.Auth;
using System.Text.Json;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IJwtService _jwtService;
        private readonly IEmailValidationService _emailValidationService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthController(
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

        // ─────────────────────────────────────────────────────────────
        // 1.1 REGISTER
        // FIX #1: Moved coach-specific validation BEFORE user creation
        //         so we never create an orphaned user in the DB.
        // FIX #2: Removed token generation — registration must NOT
        //         auto-login. Returns a success message only.
        // FIX #3: Removed Password = "" from Client entity (bad practice).
        // ─────────────────────────────────────────────────────────────
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            dto.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                ? null
                : dto.PhoneNumber.Trim();

            // Validate email format + MX records
            var emailValidation = await _emailValidationService.ValidateEmailDetailed(dto.Email);
            if (!emailValidation.isValid)
                return BadRequest(new { error = emailValidation.reason });

            // Check duplicate email
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return BadRequest(new { error = "Email is already registered" });

            // Coach certificate image is optional unless they explicitly mark themselves as certified.
            var isCoach = dto.UserType.Equals("Coach", StringComparison.OrdinalIgnoreCase);
            if (isCoach)
            {
                if (dto.IsCertified && string.IsNullOrWhiteSpace(dto.CertificateImageUrl))
                    return BadRequest(new { error = "Certificate image is required for certified coaches" });
            }

            // Create ApplicationUser
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
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            // Create profile based on user type
            if (isCoach)
            {
                var coach = new Coach
                {
                    UserId = user.Id,
                    F_name = dto.FirstName,
                    L_name = dto.LastName,
                    VerificationStatus = VerificationStatus.Pending,
                    ExperienceYears = 0,
                    NationalIdImageUrl = dto.NationalIdImageUrl ?? string.Empty,
                    IsCertified = dto.IsCertified,
                    CertificateImageUrl = dto.CertificateImageUrl
                };
                _dbContext.Coaches.Add(coach);
                // Note: Coach role is assigned AFTER admin verification
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Client");

                // FIX #3: Removed Password = "" — Identity handles passwords
                var client = new Client
                {
                    UserId = user.Id,
                    F_name = dto.FirstName,
                    L_name = dto.LastName,
                    Email = dto.Email
                };
                _dbContext.Clients.Add(client);
            }

            await _dbContext.SaveChangesAsync();

            // Send confirmation email
            try
            {
                var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = $"{Request.Scheme}://{Request.Host}/api/auth/confirm-email" +
                                       $"?userId={user.Id}&token={Uri.EscapeDataString(confirmationToken)}";
                await _emailService.SendEmailConfirmationAsync(user.Email!, dto.FirstName, confirmationLink);
            }
            catch (Exception)
            {
                // Email failure should not block registration — log in production
            }

            // ── FIX #2: Return success message only — NO tokens ──
            return Ok(new
            {
                message = isCoach
                    ? "Registration successful. Complete your coach profile, then confirm your email before logging in."
                    : "Registration successful. Please check your email to confirm your account.",
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = dto.FirstName,
                    lastName = dto.LastName,
                    userType = user.PrimaryUserType.ToString()
                }
            });
        }

        [HttpPost("coach-onboarding/complete")]
        [AllowAnonymous]
        public async Task<IActionResult> CompleteCoachOnboarding(CompleteCoachOnboardingDto dto)
        {
            var user = await _userManager.Users
                .Include(u => u.Coach)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null || user.Coach == null || user.PrimaryUserType != UserType.Coach)
            {
                return NotFound(new { error = "Coach account not found" });
            }

            if (user.IsBlocked)
            {
                return StatusCode(403, new
                {
                    error = "AccountBlocked",
                    message = "Your account has been blocked by an administrator.",
                    reason = user.BlockReason
                });
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid)
            {
                return Unauthorized(new { error = "Invalid email or password" });
            }

            var distinctSportIds = dto.Sports.Select(s => s.SportID).Distinct().ToList();
            var existingSports = await _dbContext.Sports
                .Where(s => distinctSportIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync();

            var missingSportIds = distinctSportIds.Except(existingSports).ToList();
            if (missingSportIds.Count != 0)
            {
                return BadRequest(new
                {
                    error = "One or more selected sports do not exist",
                    sportIds = missingSportIds
                });
            }

            if (!HasAnyAvailableHours(dto.AvailableHours, dto.DayHourSlots))
            {
                return BadRequest(new { error = "At least one available hour is required" });
            }

            ApplyCoachOnboarding(user.Coach, dto);

            var existingCoachSports = await _dbContext.CoachSports
                .Where(cs => cs.CoachID == user.Coach.CoachID)
                .ToListAsync();
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
                .Where(cl => cl.CoachID == user.Coach.CoachID)
                .ToListAsync();
            _dbContext.CoachLocations.RemoveRange(existingLocations);
            _dbContext.CoachLocations.Add(new CoachLocation
            {
                CoachID = user.Coach.CoachID,
                WorkingLocation = dto.City.Trim()
            });

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = user.EmailConfirmed
                    ? "Coach profile completed successfully. You can now log in once your account is verified."
                    : "Coach profile completed successfully. Please confirm your email before logging in.",
                emailConfirmed = user.EmailConfirmed,
                verificationStatus = user.Coach.VerificationStatus.ToString()
            });
        }

        // ─────────────────────────────────────────────────────────────
        // 1.2 LOGIN
        // No structural bugs — response shape matches docs.
        // Minor: renamed attemptsLeft → attemptsRemaining to match docs.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _userManager.Users
                .Include(u => u.Client)
                .Include(u => u.Coach)
                .Include(u => u.Admin)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
                return Unauthorized(new { error = "Invalid email or password" });

            if (user.IsBlocked)
            {
                return StatusCode(403, new
                {
                    error = "AccountBlocked",
                    message = "Your account has been blocked by an administrator.",
                    reason = user.BlockReason
                });
            }

            // ── Check if email is confirmed ──
            if (!user.EmailConfirmed)
            {
                return StatusCode(403, new
                {
                    error = "EmailNotConfirmed",
                    message = "Please confirm your email address before logging in. Check your inbox for the confirmation link."
                });
            }

            if (user.Coach != null)
            {
                var coachOnboardingState = await BuildCoachOnboardingStateAsync(user.Coach);

                if (!coachOnboardingState.IsComplete)
                {
                    return StatusCode(403, new
                    {
                        error = "CoachProfileIncomplete",
                        message = "Please complete the Become a Coach flow before logging in.",
                        details = new
                        {
                            coachOnboardingState.HasFullName,
                            coachOnboardingState.HasNationalId,
                            coachOnboardingState.HasAvailableDays,
                            coachOnboardingState.HasAvailableHours,
                            coachOnboardingState.HasExperienceYears,
                            coachOnboardingState.HasSportSelection,
                            coachOnboardingState.HasLocation,
                            coachOnboardingState.HasSessionPrice
                        }
                    });
                }
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                var minutesLeft = (lockoutEnd - DateTimeOffset.UtcNow)?.TotalMinutes ?? 0;

                return StatusCode(403, new
                {
                    error = $"Account locked. Try again in {Math.Ceiling(minutesLeft)} minutes",
                    attemptsRemaining = 0
                });
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid)
            {
                await _userManager.AccessFailedAsync(user);

                if (await _userManager.IsLockedOutAsync(user))
                {
                    return StatusCode(403, new
                    {
                        error = "Account locked. Try again in 15 minutes",
                        attemptsRemaining = 0
                    });
                }

                var failedAttempts = await _userManager.GetAccessFailedCountAsync(user);
                var attemptsRemaining = Math.Max(0, 5 - failedAttempts);

                return Unauthorized(new
                {
                    error = "Invalid credentials",
                    attemptsRemaining
                });
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

            return Ok(new
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

        // ─────────────────────────────────────────────────────────────
        // 1.4 REFRESH TOKEN
        // No change to logic — kept accessToken requirement (valid pattern).
        // Fixed response shape to match docs (accessToken + refreshToken only).
        // ─────────────────────────────────────────────────────────────
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh(RefreshTokenDto dto)
        {
            var userId = _jwtService.GetUserIdFromExpiredToken(dto.AccessToken);
            if (userId == null)
                return Unauthorized(new { error = "Invalid access token" });

            var refreshToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken && rt.UserId == userId);

            if (refreshToken == null || refreshToken.IsRevoked || refreshToken.ExpiresAt < DateTime.UtcNow)
                return Unauthorized(new { error = "Invalid or expired refresh token" });

            var user = await _userManager.Users
                .Include(u => u.Client)
                .Include(u => u.Coach)
                .Include(u => u.Admin)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.IsBlocked)
                return Unauthorized(new { error = "User not found or blocked" });

            var roles = await _userManager.GetRolesAsync(user);
            var newAccessToken = _jwtService.GenerateAccessToken(user, roles);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            // Rotate refresh token
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

            // FIX: Return only tokens (no full user object) — matches docs
            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken
            });
        }

        // ─────────────────────────────────────────────────────────────
        // 1.5 LOGOUT
        // FIX: Changed [FromBody] string to a proper DTO to avoid
        //      Swagger/JSON deserialization issues with raw strings.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized();

            var token = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken && rt.UserId == userId);

            if (token != null)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new { message = "Logged out successfully" });
        }

        // ─────────────────────────────────────────────────────────────
        // 1.6 GET CURRENT USER (/me)
        // FIX: Added verificationStatus for coaches so Flutter app knows
        //      if coach is Pending / Approved / Rejected.
        //      Added profilePicture field.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized();

            var user = await _userManager.Users
                .Include(u => u.Client)
                .Include(u => u.Coach)
                .Include(u => u.Admin)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(new { error = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);

            CoachOnboardingState? coachOnboardingState = null;
            if (user.Coach != null)
            {
                coachOnboardingState = await BuildCoachOnboardingStateAsync(user.Coach);
            }

            return Ok(new
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
                verificationStatus = user.Coach != null
                    ? user.Coach.VerificationStatus.ToString()
                    : null,
                coachSetupCompleted = coachOnboardingState != null
                    ? coachOnboardingState.IsComplete
                    : (bool?)null
            });
        }

        // ─────────────────────────────────────────────────────────────
        // 1.7 FORGOT PASSWORD
        // FIX: Removed resetToken from response body — CRITICAL security
        //      vulnerability. Code is now sent via email only.
        //      Added proper email sending call.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            // Always return same message — prevents user enumeration
            if (user == null)
                return Ok(new { message = "If the email exists, a reset code has been sent" });

            // Generate 6-digit code
            var random = new Random();
            var resetCode = random.Next(100000, 999999).ToString();

            // Invalidate existing unused codes
            var existingTokens = await _dbContext.PasswordResetTokens
                .Where(prt => prt.UserId == user.Id && !prt.IsUsed)
                .ToListAsync();

            foreach (var t in existingTokens)
            {
                t.IsUsed = true;
                t.UsedAt = DateTime.UtcNow;
            }

            _dbContext.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                Token = resetCode,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            // FIX: Send code via email — NEVER expose in response
            try
            {
                var firstName = user.Client?.F_name ?? user.Coach?.F_name ?? user.Email;
                await _emailService.SendPasswordResetAsync(user.Email!, firstName!, resetCode);
            }
            catch (Exception)
            {
                // Log in production — don't expose error details
            }

            return Ok(new { message = "If the email exists, a reset code has been sent" });
        }

        // ─────────────────────────────────────────────────────────────
        // 1.8 RESET PASSWORD
        // No bugs — logic is correct. Minor: unified error message.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return BadRequest(new { error = "Invalid email or token" });

            var resetToken = await _dbContext.PasswordResetTokens
                .FirstOrDefaultAsync(prt =>
                    prt.UserId == user.Id &&
                    prt.Token == dto.Token &&
                    !prt.IsUsed &&
                    prt.ExpiresAt > DateTime.UtcNow);

            if (resetToken == null)
                return BadRequest(new { error = "Invalid or expired reset token" });

            var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, identityToken, dto.NewPassword);

            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            resetToken.IsUsed = true;
            resetToken.UsedAt = DateTime.UtcNow;

            // Revoke all refresh tokens — force re-login on all devices
            var refreshTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
                .ToListAsync();

            foreach (var rt in refreshTokens)
            {
                rt.IsRevoked = true;
                rt.RevokedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Password reset successfully" });
        }

        // ─────────────────────────────────────────────────────────────
        // 1.9 CHANGE PASSWORD
        // No bugs — logic is correct.
        // ─────────────────────────────────────────────────────────────
        [HttpPut("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return NotFound(new { error = "User not found" });

            var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, dto.CurrentPassword);
            if (!isCurrentPasswordValid)
                return BadRequest(new { error = "Current password is incorrect" });

            if (dto.CurrentPassword == dto.NewPassword)
                return BadRequest(new { error = "New password must be different from current password" });

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            // Revoke all refresh tokens
            var refreshTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in refreshTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Password changed successfully. Please login again." });
        }

        // ─────────────────────────────────────────────────────────────
        // 1.10 CONFIRM EMAIL
        // No bugs — logic is correct.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(int userId, string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest(new { error = "Invalid confirmation token" });

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return NotFound(new { error = "User not found" });

            if (user.EmailConfirmed)
                return Ok(new { message = "Email already confirmed" });

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
                return BadRequest(new { error = "Email confirmation failed", details = result.Errors.Select(e => e.Description) });

            return Ok(new { message = "Email confirmed successfully! You can now login." });
        }

        // ─────────────────────────────────────────────────────────────
        // 1.11 RESEND CONFIRMATION EMAIL
        // FIX: "Email already confirmed" leaked user existence info.
        //      Now returns generic message for already-confirmed case too.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("resend-confirmation")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendConfirmationDto dto)
        {
            // Generic message in all non-error cases — prevents user enumeration
            const string genericMessage = "If the email exists, a confirmation link has been sent";

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Ok(new { message = genericMessage });

            // FIX: Don't reveal already-confirmed status — return same generic message
            if (user.EmailConfirmed)
                return Ok(new { message = genericMessage });

            var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = $"{Request.Scheme}://{Request.Host}/api/auth/confirm-email" +
                                   $"?userId={user.Id}&token={Uri.EscapeDataString(confirmationToken)}";

            try
            {
                var firstName = user.Client?.F_name ?? user.Coach?.F_name ?? user.Email;
                await _emailService.SendEmailConfirmationAsync(user.Email!, firstName!, confirmationLink);
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Failed to send confirmation email" });
            }

            return Ok(new { message = genericMessage });
        }

        // ─────────────────────────────────────────────────────────────
        // 1.3 GOOGLE LOGIN
        // No critical bugs — logic matches docs.
        // Minor: Coach via Google skips NationalId (by design — noted).
        // ─────────────────────────────────────────────────────────────
        [HttpPost("google-login")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLogin(GoogleLoginDto dto)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { _configuration["GoogleAuth:ClientId"]! }
                    });

                if (payload == null)
                    return Unauthorized(new { error = "Invalid Google token" });

                var user = await _userManager.FindByEmailAsync(payload.Email);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        Email = payload.Email,
                        UserName = payload.Email,
                        EmailConfirmed = true, // Google already verified
                        PrimaryUserType = dto.UserType == "Coach" ? UserType.Coach : UserType.Client,
                        CreatedAt = DateTime.UtcNow
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                        return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });

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
                else
                {
                    if (user.IsBlocked)
                    {
                        return StatusCode(403, new
                        {
                            error = "AccountBlocked",
                            message = "Your account has been blocked by an administrator.",
                            reason = user.BlockReason
                        });
                    }
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

                // Reload with profiles
                user = await _userManager.Users
                    .Include(u => u.Client)
                    .Include(u => u.Coach)
                    .FirstOrDefaultAsync(u => u.Id == user.Id);

                return Ok(new
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
                return BadRequest(new { error = "Google authentication failed", details = ex.Message });
            }
        }

        private static void ApplyCoachOnboarding(Coach coach, CompleteCoachOnboardingDto dto)
        {
            var parts = dto.FullName.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 1)
            {
                coach.F_name = parts[0];
            }
            else
            {
                coach.F_name = parts[0];
                coach.L_name = string.Join(" ", parts.Skip(1));
            }

            coach.ID = dto.NationalId.Trim();
            coach.Bio = dto.Bio?.Trim();
            coach.ExperienceYears = dto.ExperienceYears;
            coach.CertificateUrl = dto.CertificateUrl?.Trim();
            coach.AvailabilityStatus = SerializeAvailability(dto.AvailableDays, dto.AvailableHours, dto.DayHourSlots);
        }

        private async Task<CoachOnboardingState> BuildCoachOnboardingStateAsync(Coach coach)
        {
            var sportRows = await _dbContext.CoachSports
                .Where(cs => cs.CoachID == coach.CoachID)
                .ToListAsync();

            var hasLocation = await _dbContext.CoachLocations
                .AnyAsync(cl => cl.CoachID == coach.CoachID && !string.IsNullOrWhiteSpace(cl.WorkingLocation));

            var hasFullName = !string.IsNullOrWhiteSpace(coach.F_name);
            var hasNationalId = !string.IsNullOrWhiteSpace(coach.ID);
            var availability = ParseAvailability(coach.AvailabilityStatus);
            var hasAvailableDays = availability.AvailableDays.Any();
            var hasAvailableHours = availability.AvailableHours.Any() || availability.DayHourSlots.Any(slot => slot.Hours.Any());
            var hasExperienceYears = coach.ExperienceYears.HasValue;
            var hasSportSelection = sportRows.Count > 0;
            var hasSessionPrice = sportRows.Any(cs => cs.PricePerSession > 0);

            return new CoachOnboardingState(
                hasFullName,
                hasNationalId,
                hasAvailableDays,
                hasAvailableHours,
                hasExperienceYears,
                hasSportSelection,
                hasLocation,
                hasSessionPrice);
        }

        private static string SerializeAvailability(
            IEnumerable<string>? availableDays,
            IEnumerable<string>? availableHours,
            IEnumerable<CoachAvailabilitySlotDto>? dayHourSlots)
        {
            var requestedDays = (availableDays ?? Enumerable.Empty<string>())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var normalizedSlots = (dayHourSlots ?? Enumerable.Empty<CoachAvailabilitySlotDto>())
                .Where(slot => !string.IsNullOrWhiteSpace(slot.Day))
                .Select(slot => new DayHourSlot
                {
                    Day = slot.Day.Trim(),
                    Hours = (slot.Hours ?? new List<string>())
                        .Where(h => !string.IsNullOrWhiteSpace(h))
                        .Select(h => h.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .Where(slot => slot.Hours.Count > 0)
                .GroupBy(slot => slot.Day, StringComparer.OrdinalIgnoreCase)
                .Select(group => new DayHourSlot
                {
                    Day = group.First().Day,
                    Hours = group.SelectMany(slot => slot.Hours)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .ToList();

            var days = normalizedSlots.Select(slot => slot.Day)
                .Concat(requestedDays)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var fallbackHours = (availableHours ?? Enumerable.Empty<string>())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => h.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedSlots.Count == 0 && days.Count > 0 && fallbackHours.Count > 0)
            {
                normalizedSlots = days.Select(day => new DayHourSlot
                {
                    Day = day,
                    Hours = fallbackHours.ToList()
                }).ToList();
            }

            var hours = normalizedSlots.SelectMany(slot => slot.Hours)
                .Concat(fallbackHours)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return JsonSerializer.Serialize(new AvailabilityPayload
            {
                AvailableDays = days,
                AvailableHours = hours,
                DayHourSlots = normalizedSlots
            });
        }

        private static bool HasAnyAvailableHours(IEnumerable<string>? availableHours, IEnumerable<CoachAvailabilitySlotDto>? dayHourSlots)
        {
            return (availableHours ?? Enumerable.Empty<string>()).Any(h => !string.IsNullOrWhiteSpace(h)) ||
                   (dayHourSlots ?? Enumerable.Empty<CoachAvailabilitySlotDto>())
                       .Any(slot => (slot.Hours ?? new List<string>()).Any(h => !string.IsNullOrWhiteSpace(h)));
        }

        private static AvailabilityPayload ParseAvailability(string? availabilityStatus)
        {
            if (string.IsNullOrWhiteSpace(availabilityStatus))
            {
                return new AvailabilityPayload();
            }

            var trimmed = availabilityStatus.Trim();
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<AvailabilityPayload>(trimmed) ?? new AvailabilityPayload();
                    payload.AvailableDays ??= new List<string>();
                    payload.AvailableHours ??= new List<string>();
                    payload.DayHourSlots ??= new List<DayHourSlot>();
                    if (payload.DayHourSlots.Count == 0 && payload.AvailableDays.Count > 0)
                    {
                        payload.DayHourSlots = payload.AvailableDays.Select(day => new DayHourSlot
                        {
                            Day = day,
                            Hours = payload.AvailableHours.ToList()
                        }).ToList();
                    }
                    return payload;
                }
                catch
                {
                    return new AvailabilityPayload();
                }
            }

            var legacyDays = trimmed
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AvailabilityPayload
            {
                AvailableDays = legacyDays,
                DayHourSlots = legacyDays.Select(day => new DayHourSlot
                {
                    Day = day,
                    Hours = new List<string>()
                }).ToList()
            };
        }

        private sealed class DayHourSlot
        {
            public string Day { get; set; } = string.Empty;
            public List<string> Hours { get; set; } = new();
        }

        private sealed class AvailabilityPayload
        {
            public List<string> AvailableDays { get; set; } = new();
            public List<string> AvailableHours { get; set; } = new();
            public List<DayHourSlot> DayHourSlots { get; set; } = new();
        }

        private sealed record CoachOnboardingState(
            bool HasFullName,
            bool HasNationalId,
            bool HasAvailableDays,
            bool HasAvailableHours,
            bool HasExperienceYears,
            bool HasSportSelection,
            bool HasLocation,
            bool HasSessionPrice)
        {
            public bool IsComplete =>
                HasFullName &&
                HasNationalId &&
                HasAvailableDays &&
                HasAvailableHours &&
                HasExperienceYears &&
                HasSportSelection &&
                HasLocation &&
                HasSessionPrice;
        }
    }
}
