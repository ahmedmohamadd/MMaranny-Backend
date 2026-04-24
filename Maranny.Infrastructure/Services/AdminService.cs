using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Admin;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Services
{
    public class AdminService : IAdminService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;

        public AdminService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _dbContext = dbContext;
        }

        public async Task<(bool success, string message)> BlockUserAsync(int adminId, int userId, BlockUserDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return (false, "User not found");
            if (user.IsBlocked) return (false, "User is already blocked");

            user.IsBlocked = true;
            user.BlockReason = dto.Reason;
            user.BlockedByAdminId = adminId;
            user.BlockedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

            var tokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked).ToListAsync();
            foreach (var token in tokens) { token.IsRevoked = true; token.RevokedAt = DateTime.UtcNow; }
            await _dbContext.SaveChangesAsync();

            return (true, "User blocked successfully");
        }

        public async Task<(bool success, string message)> UnblockUserAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return (false, "User not found");
            if (!user.IsBlocked) return (false, "User is not blocked");

            user.IsBlocked = false;
            user.BlockReason = null;
            user.BlockedByAdminId = null;
            user.BlockedAt = null;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

            return (true, "User unblocked successfully");
        }

        public async Task<object> GetPendingCoachesAsync()
        {
            return await _dbContext.Coaches
                .Include(c => c.User)
                .Where(c => c.VerificationStatus == VerificationStatus.Pending)
                .Select(c => new
                {
                    c.CoachID,
                    c.F_name,
                    c.L_name,
                    c.Bio,
                    c.ExperienceYears,
                    c.CertificateUrl,
                    Email = c.User.Email,
                    PhoneNumber = c.User.PhoneNumber,
                    CreatedAt = c.User.CreatedAt
                }).ToListAsync();
        }

        public async Task<(bool success, string message)> VerifyCoachAsync(int adminId, int coachId, VerifyCoachDto dto)
        {
            var coach = await _dbContext.Coaches.Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CoachID == coachId);
            if (coach == null) return (false, "Coach not found");
            if (coach.VerificationStatus == VerificationStatus.Approved)
                return (false, "Coach is already verified");

            coach.VerificationStatus = VerificationStatus.Approved;
            coach.VerifiedAt = DateTime.UtcNow;
            coach.VerifiedByAdminId = adminId;
            coach.VerificationNotes = dto.Notes;
            coach.RejectionReason = null;

            var user = coach.User;
            if (!await _userManager.IsInRoleAsync(user, "Coach"))
                await _userManager.AddToRoleAsync(user, "Coach");

            if (user.PrimaryUserType != UserType.Coach)
            {
                user.PrimaryUserType = UserType.Coach;
                await _userManager.UpdateAsync(user);
            }

            await _dbContext.SaveChangesAsync();
            return (true, "Coach verified successfully");
        }

        public async Task<(bool success, string message)> RejectCoachAsync(int coachId, RejectCoachDto dto)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.CoachID == coachId);
            if (coach == null) return (false, "Coach not found");
            if (coach.VerificationStatus == VerificationStatus.Approved)
                return (false, "Cannot reject an already verified coach");

            coach.VerificationStatus = VerificationStatus.Rejected;
            coach.RejectionReason = dto.Reason;
            coach.VerificationNotes = null;
            coach.VerifiedAt = null;
            coach.VerifiedByAdminId = null;

            await _dbContext.SaveChangesAsync();
            return (true, "Coach verification rejected");
        }

        public async Task<(bool success, object? data)> GetUserDetailsAsync(int userId)
        {
            var user = await _userManager.Users
                .Include(u => u.Client).Include(u => u.Coach).Include(u => u.Admin)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return (false, null);

            var roles = await _userManager.GetRolesAsync(user);

            return (true, new
            {
                user.Id,
                user.Email,
                user.PhoneNumber,
                user.EmailConfirmed,
                user.PhoneNumberConfirmed,
                primaryUserType = user.PrimaryUserType.ToString(),
                user.IsBlocked,
                user.BlockReason,
                user.BlockedAt,
                user.CreatedAt,
                Roles = roles,
                ClientProfile = user.Client != null ? new
                {
                    user.Client.ClientID,
                    user.Client.F_name,
                    user.Client.L_name,
                    user.Client.City,
                    user.Client.Gender
                } : null,
                CoachProfile = user.Coach != null ? new
                {
                    user.Coach.CoachID,
                    user.Coach.F_name,
                    user.Coach.L_name,
                    user.Coach.Bio,
                    user.Coach.ExperienceYears,
                    verificationStatus = user.Coach.VerificationStatus.ToString(),
                    user.Coach.VerifiedAt,
                    user.Coach.RejectionReason
                } : null,
                AdminProfile = user.Admin != null ? new
                {
                    user.Admin.AdminID,
                    user.Admin.F_name,
                    user.Admin.L_name,
                    user.Admin.Username
                } : null
            });
        }

        public async Task<object> GetUsersAsync(string? role, bool? isBlocked, int page, int pageSize)
        {
            var query = _userManager.Users
                .Include(u => u.Client).Include(u => u.Coach).Include(u => u.Admin)
                .AsQueryable();

            if (isBlocked.HasValue)
                query = query.Where(u => u.IsBlocked == isBlocked.Value);

            var allUsers = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

            var userList = new List<object>();
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!string.IsNullOrEmpty(role) && !roles.Contains(role)) continue;

                userList.Add(new
                {
                    id = user.Id,
                    email = user.Email,
                    name = user.Client?.F_name ?? user.Coach?.F_name ?? user.Admin?.F_name ?? "",
                    primaryUserType = user.PrimaryUserType.ToString(),
                    roles,
                    isBlocked = user.IsBlocked,
                    emailConfirmed = user.EmailConfirmed,
                    createdAt = user.CreatedAt
                });
            }

            var totalCount = userList.Count;
            var paged = userList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                users = paged
            };
        }

        public async Task<object> GetPendingCertificatesAsync()
        {
            return await _dbContext.Coaches
                .Include(c => c.User)
                .Where(c => c.VerificationStatus == VerificationStatus.Pending &&
                            !string.IsNullOrEmpty(c.CertificateUrl))
                .Select(c => new
                {
                    c.CoachID,
                    c.F_name,
                    c.L_name,
                    c.Bio,
                    c.ExperienceYears,
                    c.CertificateUrl,
                    Email = c.User.Email,
                    PhoneNumber = c.User.PhoneNumber,
                    CreatedAt = c.User.CreatedAt
                }).ToListAsync();
        }

        public async Task<(bool success, string message)> VerifyCertificateAsync(int adminId, int coachId, string? notes)
        {
            var coach = await _dbContext.Coaches.Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CoachID == coachId);
            if (coach == null) return (false, "Coach not found");
            if (string.IsNullOrEmpty(coach.CertificateUrl)) return (false, "No certificate uploaded");

            coach.VerificationStatus = VerificationStatus.Approved;
            coach.VerifiedAt = DateTime.UtcNow;
            coach.VerifiedByAdminId = adminId;
            coach.VerificationNotes = notes;

            var user = coach.User;
            if (!await _userManager.IsInRoleAsync(user, "Coach"))
                await _userManager.AddToRoleAsync(user, "Coach");

            if (user.PrimaryUserType != UserType.Coach)
            {
                user.PrimaryUserType = UserType.Coach;
                await _userManager.UpdateAsync(user);
            }

            await _dbContext.SaveChangesAsync();
            return (true, "Certificate verified successfully");
        }

        public async Task<object> GetPendingReviewsAsync(int page, int pageSize)
        {
            var query = _dbContext.Reviews
                .Include(r => r.Client).Include(r => r.Coach)
                .OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var reviews = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new
                {
                    r.ReviewID,
                    r.Rating,
                    r.Comment,
                    r.CoachResponse,
                    r.CreatedAt,
                    Client = new { r.Client.ClientID, Name = r.Client.F_name + " " + r.Client.L_name, r.Client.Email },
                    Coach = new { r.Coach.CoachID, Name = r.Coach.F_name + " " + r.Coach.L_name }
                }).ToListAsync();

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                reviews
            };
        }

        public async Task<(bool success, string message)> ModerateReviewAsync(int reviewId, string action)
        {
            var review = await _dbContext.Reviews.FindAsync(reviewId);
            if (review == null) return (false, "Review not found");

            if (action.ToLower() == "delete")
            {
                _dbContext.Reviews.Remove(review);
                await _dbContext.SaveChangesAsync();
                await UpdateCoachRatingAsync(review.CoachID);
                return (true, "Review deleted successfully");
            }

            return (false, "Invalid action");
        }

        public async Task<object> GetAnalyticsAsync()
        {
            var totalUsers = await _dbContext.Users.CountAsync();
            var totalCoaches = await _dbContext.Coaches.CountAsync();
            var verifiedCoaches = await _dbContext.Coaches.CountAsync(c => c.VerificationStatus == VerificationStatus.Approved);
            var pendingCoaches = await _dbContext.Coaches.CountAsync(c => c.VerificationStatus == VerificationStatus.Pending);
            var totalClients = await _dbContext.Clients.CountAsync();
            var totalBookings = await _dbContext.Bookings.CountAsync();
            var completedBookings = await _dbContext.Bookings.CountAsync(b => b.Status == BookingStatus.Completed);
            var totalReviews = await _dbContext.Reviews.CountAsync();
            var averageRating = await _dbContext.Coaches.Where(c => c.AvgRating > 0).AverageAsync(c => (decimal?)c.AvgRating) ?? 0;
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var newUsersThisMonth = await _dbContext.Users.CountAsync(u => u.CreatedAt >= startOfMonth);
            var totalRevenue = await _dbContext.Payments.Where(p => p.Status == PaymentStatus.Completed).SumAsync(p => (decimal?)p.Amount) ?? 0;

            return new
            {
                Users = new { Total = totalUsers, Clients = totalClients, Coaches = totalCoaches, NewThisMonth = newUsersThisMonth },
                Coaches = new { Total = totalCoaches, Verified = verifiedCoaches, Pending = pendingCoaches },
                Bookings = new { Total = totalBookings, Completed = completedBookings },
                Reviews = new { Total = totalReviews, AverageRating = Math.Round(averageRating, 2) },
                Revenue = new { Total = totalRevenue, Currency = "EGP" },
                MonthlyGrowth = new { NewUsers = newUsersThisMonth, Month = DateTime.UtcNow.ToString("MMMM yyyy") }
            };
        }

        private async Task UpdateCoachRatingAsync(int coachId)
        {
            var coach = await _dbContext.Coaches.FindAsync(coachId);
            if (coach == null) return;
            var avg = await _dbContext.Reviews.Where(r => r.CoachID == coachId).AverageAsync(r => (decimal?)r.Rating);
            coach.AvgRating = avg ?? 0;
            await _dbContext.SaveChangesAsync();
        }
    }
}