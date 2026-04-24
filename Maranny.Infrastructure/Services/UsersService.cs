using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Profile;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Maranny.Infrastructure.Services
{
    public class UsersService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;

        public UsersService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _dbContext = dbContext;
        }

        public async Task<(bool success, string message)> UpdateProfileAsync(int userId, UpdateProfileDto dto)
        {
            var user = await _userManager.Users
                .Include(u => u.Client)
                .Include(u => u.Coach)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return (false, "User not found");

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                user.PhoneNumber = dto.PhoneNumber;
                await _userManager.UpdateAsync(user);
            }

            if (user.Client != null)
            {
                if (!string.IsNullOrWhiteSpace(dto.FirstName)) user.Client.F_name = dto.FirstName;
                if (!string.IsNullOrWhiteSpace(dto.LastName)) user.Client.L_name = dto.LastName;
                if (!string.IsNullOrWhiteSpace(dto.City)) user.Client.City = dto.City;
                if (!string.IsNullOrWhiteSpace(dto.Street)) user.Client.Street_name = dto.Street;
                if (!string.IsNullOrWhiteSpace(dto.BuildingNumber)) user.Client.Build_num = dto.BuildingNumber;
                if (dto.DateOfBirth.HasValue) user.Client.Date_of_Birth = dto.DateOfBirth.Value;
                if (!string.IsNullOrWhiteSpace(dto.Gender) && Enum.TryParse<Gender>(dto.Gender, out var cGender))
                    user.Client.Gender = cGender;
            }

            if (user.Coach != null)
            {
                if (!string.IsNullOrWhiteSpace(dto.FirstName)) user.Coach.F_name = dto.FirstName;
                if (!string.IsNullOrWhiteSpace(dto.LastName)) user.Coach.L_name = dto.LastName;
                if (!string.IsNullOrWhiteSpace(dto.Bio)) user.Coach.Bio = dto.Bio;
                if (dto.ExperienceYears.HasValue) user.Coach.ExperienceYears = dto.ExperienceYears.Value;
                if (!string.IsNullOrWhiteSpace(dto.CertificateUrl)) user.Coach.CertificateUrl = dto.CertificateUrl;
                if (!string.IsNullOrWhiteSpace(dto.Gender) && Enum.TryParse<Gender>(dto.Gender, out var coGender))
                    user.Coach.Gender = coGender;
            }

            await _dbContext.SaveChangesAsync();
            return (true, "Profile updated successfully");
        }

        public async Task<(bool success, string message, object? data)> UpdatePreferencesAsync(int userId, UpdatePreferencesDto dto)
        {
            var preferences = await _dbContext.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var hasStructured = dto.Sports != null ||
                !string.IsNullOrWhiteSpace(dto.City) || !string.IsNullOrWhiteSpace(dto.Area) ||
                !string.IsNullOrWhiteSpace(dto.LocationPreference) || !string.IsNullOrWhiteSpace(dto.RatingPreference) ||
                !string.IsNullOrWhiteSpace(dto.CoachGender) || !string.IsNullOrWhiteSpace(dto.CoachAgeRange) ||
                dto.CertifiedOnly.HasValue;

            string? serialized = null;
            if (hasStructured)
            {
                serialized = JsonSerializer.Serialize(new
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
                    Sports = serialized,
                    BudgetMin = dto.BudgetMin,
                    BudgetMax = dto.BudgetMax,
                    MaxDistance = dto.MaxDistance,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.UserPreferences.Add(preferences);
            }
            else
            {
                if (serialized != null) preferences.Sports = serialized;
                if (dto.BudgetMin.HasValue) preferences.BudgetMin = dto.BudgetMin;
                if (dto.BudgetMax.HasValue) preferences.BudgetMax = dto.BudgetMax;
                if (dto.MaxDistance.HasValue) preferences.MaxDistance = dto.MaxDistance;
                preferences.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            return (true, "Preferences updated successfully", new
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
            });
        }

        public async Task<(bool success, object? data)> GetCoachSetupAsync(int userId)
        {
            var coach = await _dbContext.Coaches
                .Include(c => c.CoachSports).ThenInclude(cs => cs.Sport)
                .Include(c => c.CoachLocations)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (coach == null) return (false, null);

            var availableDays = string.IsNullOrWhiteSpace(coach.AvailabilityStatus)
                ? new List<string>()
                : coach.AvailabilityStatus.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            return (true, new
            {
                coach.CoachID,
                fullName = $"{coach.F_name} {coach.L_name}".Trim(),
                nationalId = coach.ID,
                city = coach.CoachLocations.Select(cl => cl.WorkingLocation).FirstOrDefault(),
                sessionPrice = coach.CoachSports.Where(cs => cs.PricePerSession.HasValue)
                    .OrderBy(cs => cs.PricePerSession).Select(cs => cs.PricePerSession).FirstOrDefault(),
                firstName = coach.F_name,
                lastName = coach.L_name,
                coach.Bio,
                coach.ExperienceYears,
                coach.CertificateUrl,
                verificationStatus = coach.VerificationStatus.ToString(),
                availableDays,
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

        public async Task<(bool success, string message)> UpdateCoachSetupAsync(int userId, UpdateCoachSetupDto dto)
        {
            var coach = await _dbContext.Coaches
                .Include(c => c.CoachSports)
                .Include(c => c.CoachLocations)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (coach == null) return (false, "Coach profile not found");

            var distinctSportIds = dto.Sports.Select(s => s.SportID).Distinct().ToList();
            var existingSports = await _dbContext.Sports
                .Where(s => distinctSportIds.Contains(s.Id)).Select(s => s.Id).ToListAsync();
            var missingSportIds = distinctSportIds.Except(existingSports).ToList();
            if (missingSportIds.Count != 0)
                return (false, $"Sports not found: {string.Join(", ", missingSportIds)}");

            if (!string.IsNullOrWhiteSpace(dto.FullName))
            {
                var parts = dto.FullName.Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                coach.F_name = parts[0];
                coach.L_name = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";
            }

            if (!string.IsNullOrWhiteSpace(dto.NationalId)) coach.ID = dto.NationalId.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Bio)) coach.Bio = dto.Bio.Trim();
            if (dto.ExperienceYears.HasValue) coach.ExperienceYears = dto.ExperienceYears.Value;
            if (!string.IsNullOrWhiteSpace(dto.CertificateUrl)) coach.CertificateUrl = dto.CertificateUrl.Trim();

            coach.AvailabilityStatus = string.Join(",",
                dto.AvailableDays.Where(d => !string.IsNullOrWhiteSpace(d))
                    .Select(d => d.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));

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
                .Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (!string.IsNullOrWhiteSpace(dto.City) &&
                !normalizedLocations.Contains(dto.City.Trim(), StringComparer.OrdinalIgnoreCase))
                normalizedLocations.Insert(0, dto.City.Trim());

            _dbContext.CoachLocations.RemoveRange(coach.CoachLocations);
            coach.CoachLocations = normalizedLocations
                .Select(l => new CoachLocation { CoachID = coach.CoachID, WorkingLocation = l }).ToList();

            await _dbContext.SaveChangesAsync();
            return (true, "Coach setup updated successfully");
        }

        public async Task<(bool success, string message, object? data)> UploadProfileImageAsync(
    int userId, Stream fileStream, string fileName, long fileSize)
        {
            if (fileStream == null || fileSize == 0) return (false, "No image file provided", null);
            if (fileSize > 5 * 1024 * 1024) return (false, "Image size cannot exceed 5MB", null);

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return (false, "Only JPG, PNG, and GIF images are allowed", null);

            try
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{userId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await fileStream.CopyToAsync(stream);

                var imageUrl = $"/uploads/profiles/{uniqueFileName}";

                var user = await _userManager.Users
                    .Include(u => u.Client).Include(u => u.Coach)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return (false, "User not found", null);

                if (user.Client != null) user.Client.URL = imageUrl;
                if (user.Coach != null) user.Coach.URL = imageUrl;

                await _dbContext.SaveChangesAsync();
                return (true, "Profile image uploaded successfully", new { imageUrl });
            }
            catch (Exception ex)
            {
                return (false, $"Failed to upload image: {ex.Message}", null);
            }
        }
    }
}