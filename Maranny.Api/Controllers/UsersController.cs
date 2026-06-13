using Maranny.Application.DTOs.Profile;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _dbContext = dbContext;
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.Users
                .Include(u => u.Client)
                .Include(u => u.Coach)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            // Update phone number in ApplicationUser
            if (dto.PhoneNumber != null)
            {
                user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                    ? null
                    : dto.PhoneNumber.Trim();
                await _userManager.UpdateAsync(user);
            }

            // Update Client profile
            if (user.Client != null)
            {
                if (!string.IsNullOrWhiteSpace(dto.FirstName))
                    user.Client.F_name = dto.FirstName;
                if (!string.IsNullOrWhiteSpace(dto.LastName))
                    user.Client.L_name = dto.LastName;

                if (dto.City != null)
                    user.Client.City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim();

                if (dto.Street != null)
                    user.Client.Street_name = string.IsNullOrWhiteSpace(dto.Street) ? null : dto.Street.Trim();

                if (dto.BuildingNumber != null)
                    user.Client.Build_num = string.IsNullOrWhiteSpace(dto.BuildingNumber) ? null : dto.BuildingNumber.Trim();

                if (dto.Bio != null)
                    user.Client.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();

                if (dto.DateOfBirth.HasValue)
                    user.Client.Date_of_Birth = dto.DateOfBirth.Value;

                if (!string.IsNullOrWhiteSpace(dto.Gender) && Enum.TryParse<Gender>(dto.Gender, out var clientGender))
                    user.Client.Gender = clientGender;

            }

            // Update Coach profile
            if (user.Coach != null)
            {
                if (!string.IsNullOrWhiteSpace(dto.FirstName))
                    user.Coach.F_name = dto.FirstName;
                if (!string.IsNullOrWhiteSpace(dto.LastName))
                    user.Coach.L_name = dto.LastName;

                if (!string.IsNullOrWhiteSpace(dto.Bio))
                    user.Coach.Bio = dto.Bio;

                if (dto.ExperienceYears.HasValue)
                    user.Coach.ExperienceYears = dto.ExperienceYears.Value;

                if (!string.IsNullOrWhiteSpace(dto.CertificateUrl))
                    user.Coach.CertificateUrl = dto.CertificateUrl;
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Profile updated successfully" });
        }

        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var preferences = await _dbContext.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (preferences == null)
            {
                return Ok(new
                {
                    sports = Array.Empty<string>(),
                    budgetMin = (decimal?)null,
                    budgetMax = (decimal?)null,
                    maxDistance = (decimal?)null,
                    city = (string?)null,
                    area = (string?)null,
                    locationPreference = (string?)null,
                    ratingPreference = (string?)null,
                    coachGender = (string?)null,
                    coachAgeRange = (string?)null,
                    certifiedOnly = false
                });
            }

            var details = ParseStoredPreferences(preferences.Sports);

            return Ok(new
            {
                sports = details.Sports,
                budgetMin = preferences.BudgetMin,
                budgetMax = preferences.BudgetMax,
                maxDistance = preferences.MaxDistance,
                city = details.City,
                area = details.Area,
                locationPreference = details.LocationPreference,
                ratingPreference = details.RatingPreference,
                coachGender = details.CoachGender,
                coachAgeRange = details.CoachAgeRange,
                certifiedOnly = details.CertifiedOnly
            });
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences(UpdatePreferencesDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var preferences = await _dbContext.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var hasStructuredPreferences =
                dto.Sports != null ||
                !string.IsNullOrWhiteSpace(dto.City) ||
                !string.IsNullOrWhiteSpace(dto.Area) ||
                !string.IsNullOrWhiteSpace(dto.LocationPreference) ||
                !string.IsNullOrWhiteSpace(dto.RatingPreference) ||
                !string.IsNullOrWhiteSpace(dto.CoachGender) ||
                !string.IsNullOrWhiteSpace(dto.CoachAgeRange) ||
                dto.CertifiedOnly.HasValue;

            string? serializedPreferences = null;
            if (hasStructuredPreferences)
            {
                serializedPreferences = JsonSerializer.Serialize(new
                {
                    sports = dto.Sports ?? new List<string>(),
                    city = dto.City?.Trim(),
                    area = dto.Area?.Trim(),
                    locationPreference = dto.LocationPreference?.Trim(),
                    ratingPreference = dto.RatingPreference?.Trim(),
                    coachGender = dto.CoachGender?.Trim(),
                    coachAgeRange = dto.CoachAgeRange?.Trim(),
                    certifiedOnly = dto.CertifiedOnly ?? false
                });
            }

            if (preferences == null)
            {
                preferences = new UserPreferences
                {
                    UserId = userId,
                    Sports = serializedPreferences,
                    BudgetMin = dto.BudgetMin,
                    BudgetMax = dto.BudgetMax,
                    MaxDistance = dto.MaxDistance,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.UserPreferences.Add(preferences);
            }
            else
            {
                if (serializedPreferences != null)
                    preferences.Sports = serializedPreferences;

                if (dto.BudgetMin.HasValue)
                    preferences.BudgetMin = dto.BudgetMin;

                if (dto.BudgetMax.HasValue)
                    preferences.BudgetMax = dto.BudgetMax;

                if (dto.MaxDistance.HasValue)
                    preferences.MaxDistance = dto.MaxDistance;

                preferences.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Preferences updated successfully",
                savedPreferences = new
                {
                    dto.Sports,
                    dto.BudgetMin,
                    dto.BudgetMax,
                    dto.MaxDistance,
                    dto.City,
                    dto.Area,
                    dto.LocationPreference,
                    dto.RatingPreference,
                    dto.CoachGender,
                    dto.CoachAgeRange,
                    dto.CertifiedOnly
                }
            });
        }

        [HttpGet("coach-setup")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GetCoachSetup()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches
                .Include(c => c.CoachSports)
                    .ThenInclude(cs => cs.Sport)
                .Include(c => c.CoachLocations)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            var availability = ParseAvailability(coach.AvailabilityStatus);
            var availableDays = availability.AvailableDays;
            var availableHours = availability.AvailableHours;

            return Ok(new
            {
                coach.CoachID,
                fullName = $"{coach.F_name} {coach.L_name}".Trim(),
                nationalId = coach.ID,
                city = coach.CoachLocations.Select(cl => cl.WorkingLocation).FirstOrDefault(),
                sessionPrice = coach.CoachSports
                    .Where(cs => cs.PricePerSession.HasValue)
                    .OrderBy(cs => cs.PricePerSession)
                    .Select(cs => cs.PricePerSession)
                    .FirstOrDefault(),
                firstName = coach.F_name,
                lastName = coach.L_name,
                coach.Bio,
                coach.ExperienceYears,
                gender = coach.Gender.HasValue ? coach.Gender.ToString() : null,
                coach.Age,
                coach.CertificateUrl,
                coach.CertificateImageUrl,
                verificationStatus = coach.VerificationStatus.ToString(),
                availableDays,
                availableHours,
                dayHourSlots = availability.DayHourSlots,
                sports = coach.CoachSports.Select(cs => new
                {
                    cs.SportID,
                    sportName = cs.Sport.Name,
                    cs.Description,
                    cs.PricePerSession,
                    cs.ExperienceYears
                }),
                locations = coach.CoachLocations.Select(cl => cl.WorkingLocation)
            });
        }

        [HttpPut("coach-setup")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> UpdateCoachSetup(UpdateCoachSetupDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches
                .Include(c => c.CoachSports)
                .Include(c => c.CoachLocations)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            var distinctSportIds = dto.Sports.Select(s => s.SportID).Distinct().ToList();
            var existingSports = await _dbContext.Sports
                .Where(s => distinctSportIds.Contains(s.Id) && s.Name != "Yoga")
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

            if (!string.IsNullOrWhiteSpace(dto.FullName))
            {
                var parts = dto.FullName.Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length == 1)
                {
                    coach.F_name = parts[0];
                }
                else if (parts.Length > 1)
                {
                    coach.F_name = parts[0];
                    coach.L_name = string.Join(" ", parts.Skip(1));
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.NationalId))
                coach.ID = dto.NationalId.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Bio))
                coach.Bio = dto.Bio.Trim();

            if (dto.ExperienceYears.HasValue)
                coach.ExperienceYears = dto.ExperienceYears.Value;

            if (!string.IsNullOrWhiteSpace(dto.CertificateUrl))
                coach.CertificateUrl = dto.CertificateUrl.Trim();

            if (!HasAnyAvailableHours(dto.AvailableHours, dto.DayHourSlots))
            {
                return BadRequest(new { error = "At least one available hour is required" });
            }

            coach.AvailabilityStatus = SerializeAvailability(dto.AvailableDays, dto.AvailableHours, dto.DayHourSlots);

            _dbContext.CoachSports.RemoveRange(coach.CoachSports);
            coach.CoachSports = dto.Sports.Select(s => new CoachSport
            {
                CoachID = coach.CoachID,
                SportID = s.SportID,
                Description = s.Description?.Trim(),
                PricePerSession = s.PricePerSession ?? dto.SessionPrice,
                ExperienceYears = s.ExperienceYears
            }).ToList();

            var normalizedLocations = dto.Locations
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(dto.City) &&
                !normalizedLocations.Contains(dto.City.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                normalizedLocations.Insert(0, dto.City.Trim());
            }

            _dbContext.CoachLocations.RemoveRange(coach.CoachLocations);
            coach.CoachLocations = normalizedLocations
                .Select(l => new CoachLocation
                {
                    CoachID = coach.CoachID,
                    WorkingLocation = l
                })
                .ToList();

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Coach setup updated successfully" });
        }

        private static string SerializeAvailability(
            IEnumerable<string>? availableDays,
            IEnumerable<string>? availableHours,
            IEnumerable<CoachSetupAvailabilitySlotDto>? dayHourSlots)
        {
            var requestedDays = (availableDays ?? Enumerable.Empty<string>())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var normalizedSlots = (dayHourSlots ?? Enumerable.Empty<CoachSetupAvailabilitySlotDto>())
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

        private static bool HasAnyAvailableHours(IEnumerable<string>? availableHours, IEnumerable<CoachSetupAvailabilitySlotDto>? dayHourSlots)
        {
            return (availableHours ?? Enumerable.Empty<string>()).Any(h => !string.IsNullOrWhiteSpace(h)) ||
                   (dayHourSlots ?? Enumerable.Empty<CoachSetupAvailabilitySlotDto>())
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

        private static StoredPreferenceDetails ParseStoredPreferences(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new StoredPreferenceDetails();
            }

            try
            {
                using var document = JsonDocument.Parse(raw);
                var root = document.RootElement;

                return new StoredPreferenceDetails
                {
                    Sports = ReadStringArray(root, "sports"),
                    City = ReadString(root, "city"),
                    Area = ReadString(root, "area"),
                    LocationPreference = ReadString(root, "locationPreference"),
                    RatingPreference = ReadString(root, "ratingPreference"),
                    CoachGender = ReadString(root, "coachGender"),
                    CoachAgeRange = ReadString(root, "coachAgeRange"),
                    CertifiedOnly = ReadBool(root, "certifiedOnly")
                };
            }
            catch (JsonException)
            {
                return new StoredPreferenceDetails
                {
                    Sports = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(sport => !string.IsNullOrWhiteSpace(sport))
                        .ToList()
                };
            }
        }

        private static List<string> ReadStringArray(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Array)
            {
                return new List<string>();
            }

            return property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string? ReadString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = property.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static bool ReadBool(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.True;
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

        private sealed class StoredPreferenceDetails
        {
            public List<string> Sports { get; init; } = new();
            public string? City { get; init; }
            public string? Area { get; init; }
            public string? LocationPreference { get; init; }
            public string? RatingPreference { get; init; }
            public string? CoachGender { get; init; }
            public string? CoachAgeRange { get; init; }
            public bool CertifiedOnly { get; init; }
        }

        [HttpPost("profile/image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProfileImage([FromForm] UploadImageDto dto)
        {
            // Get current user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(); 
            }

            // Validate file
            if (dto.File == null || dto.File.Length == 0)
            {
                return BadRequest(new { error = "No image file provided" });
            }

            // Validate file size (max 5MB)
            if (dto.File.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { error = "Image size cannot exceed 5MB" });
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { error = "Only JPG, PNG, and GIF images are allowed" });
            }

            try
            {
                // Create uploads directory if it doesn't exist
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                Directory.CreateDirectory(uploadsFolder);

                // Generate unique filename
                var fileName = $"{userId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.File.CopyToAsync(stream);
                }

                // Generate URL
                var imageUrl = $"/uploads/profiles/{fileName}";

                // Update user profile with image URL
                var user = await _userManager.Users
                    .Include(u => u.Client)
                    .Include(u => u.Coach)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return NotFound(new { error = "User not found" });
                }

                // Store URL in appropriate profile
                if (user.Client != null)
                {
                    user.Client.URL = imageUrl;
                }
                if (user.Coach != null)
                {
                    user.Coach.URL = imageUrl;
                }

                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    message = "Profile image uploaded successfully",
                    imageUrl = imageUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to upload image", details = ex.Message });
            }
        }
        public class UploadImageDto
        {
            public IFormFile File { get; set; } = null!;
        }

        [HttpPost("coach/certificate")]
        [Authorize(Roles = "Coach")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadCoachCertificate([FromForm] UploadImageDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            if (dto.File == null || dto.File.Length == 0)
            {
                return BadRequest(new { error = "No certificate image provided" });
            }

            if (dto.File.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { error = "Certificate image size cannot exceed 5MB" });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { error = "Only JPG, PNG, and GIF images are allowed" });
            }

            try
            {
                var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
                if (coach == null)
                {
                    return NotFound(new { error = "Coach profile not found" });
                }

                var uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads",
                    "certificates");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{userId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.File.CopyToAsync(stream);
                }

                var certificateUrl = $"/uploads/certificates/{fileName}";
                coach.CertificateUrl = certificateUrl;
                coach.CertificateImageUrl = certificateUrl;

                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    message = "Certificate uploaded successfully",
                    certificateUrl,
                    certificateImageUrl = certificateUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to upload certificate", details = ex.Message });
            }
        }

    }
}

