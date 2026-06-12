using Maranny.Application.DTOs.Admin;
using Maranny.Application.DTOs.Auth;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _dbContext = dbContext;
        }

        // Block a user
        [HttpPost("users/{userId}/block")]
        public async Task<IActionResult> BlockUser(int userId, [FromBody] BlockUserDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            if (user.IsBlocked)
            {
                return BadRequest(new { error = "User is already blocked" });
            }

            // Get admin ID from JWT
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(adminIdClaim, out int adminId))
            {
                return Unauthorized();
            }

            // Block the user
            user.IsBlocked = true;
            user.BlockReason = dto.Reason;
            user.BlockedByAdminId = adminId;
            user.BlockedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

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

            return Ok(new { message = "User blocked successfully" });
        }

        // Unblock a user
        [HttpPost("users/{userId}/unblock")]
        public async Task<IActionResult> UnblockUser(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            if (!user.IsBlocked)
            {
                return BadRequest(new { error = "User is not blocked" });
            }

            // Unblock the user
            user.IsBlocked = false;
            user.BlockReason = null;
            user.BlockedByAdminId = null;
            user.BlockedAt = null;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            return Ok(new { message = "User unblocked successfully" });
        }

        // Get list of pending coach verifications
        [HttpGet("coaches/pending")]
        public async Task<IActionResult> GetPendingCoaches()
        {
            var pendingCoaches = await _dbContext.Coaches
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
                })
                .ToListAsync();

            return Ok(pendingCoaches);
        }

        // Verify a coach
        [HttpPost("coaches/{coachId}/verify")]
        public async Task<IActionResult> VerifyCoach(int coachId, [FromBody] VerifyCoachDto dto)
        {
            var coach = await _dbContext.Coaches
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CoachID == coachId);

            if (coach == null)
            {
                return NotFound(new { error = "Coach not found" });
            }

            if (coach.VerificationStatus == VerificationStatus.Approved)
            {
                return BadRequest(new { error = "Coach is already verified" });
            }

            // Get admin ID from JWT
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(adminIdClaim, out int adminId))
            {
                return Unauthorized();
            }

            // Verify the coach
            coach.VerificationStatus = VerificationStatus.Approved;
            coach.VerifiedAt = DateTime.UtcNow;
            coach.VerifiedByAdminId = adminId;
            coach.VerificationNotes = dto.Notes;
            coach.RejectionReason = null;

            // Add Coach role to the user
            var user = coach.User;
            var hasCoachRole = await _userManager.IsInRoleAsync(user, "Coach");
            if (!hasCoachRole)
            {
                await _userManager.AddToRoleAsync(user, "Coach");
            }

            // Update primary user type if needed
            if (user.PrimaryUserType != UserType.Coach)
            {
                user.PrimaryUserType = UserType.Coach;
                await _userManager.UpdateAsync(user);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Coach verified successfully" });
        }

        // Reject a coach verification
        [HttpPost("coaches/{coachId}/reject")]
        public async Task<IActionResult> RejectCoach(int coachId, [FromBody] RejectCoachDto dto)
        {
            var coach = await _dbContext.Coaches
                .FirstOrDefaultAsync(c => c.CoachID == coachId);

            if (coach == null)
            {
                return NotFound(new { error = "Coach not found" });
            }

            if (coach.VerificationStatus == VerificationStatus.Approved)
            {
                return BadRequest(new { error = "Cannot reject an already verified coach" });
            }

            // Reject the coach
            coach.VerificationStatus = VerificationStatus.Rejected;
            coach.RejectionReason = dto.Reason;
            coach.VerificationNotes = null;
            coach.VerifiedAt = null;
            coach.VerifiedByAdminId = null;

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Coach verification rejected" });
        }

        // Get user details
        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUserDetails(int userId)
        {
            var user = await _userManager.Users
                .Include(u => u.Client)
                .Include(u => u.Coach)
                .Include(u => u.Admin)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            var roles = await _userManager.GetRolesAsync(user);

            var userDetails = new
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
            };

            return Ok(userDetails);
        }

        // List all users with filters
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? role = null,
            [FromQuery] bool? isBlocked = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _userManager.Users
     .Include(u => u.Client)
     .Include(u => u.Coach)
     .Include(u => u.Admin)
     .AsQueryable();

            if (isBlocked.HasValue)
                query = query.Where(u => u.IsBlocked == isBlocked.Value);

            var allUsers = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

            var userList = new List<object>();
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!string.IsNullOrEmpty(role) && !roles.Contains(role))
                    continue;

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
            var pagedUsers = userList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                totalCount = totalCount,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                users = pagedUsers
            });
        }

        [HttpGet("bookings/recent")]
        public async Task<IActionResult> GetRecentBookings(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var query = _dbContext.Bookings
                .Include(b => b.Client)
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Coach)
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Sport)
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Payment)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<BookingStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(b => b.Status == parsedStatus);
            }

            var totalCount = await query.CountAsync();

            var bookings = await query
                .OrderByDescending(b => b.BookingDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    bookingId = b.BookingID,
                    bookingDate = b.BookingDate,
                    status = b.Status.ToString(),
                    amount = b.TrainingSession.Payment != null
                        ? b.TrainingSession.Payment.Amount
                        : 0,
                    client = new
                    {
                        clientId = b.Client.ClientID,
                        name = (b.Client.F_name + " " + b.Client.L_name).Trim()
                    },
                    coach = new
                    {
                        coachId = b.TrainingSession.Coach.CoachID,
                        name = (b.TrainingSession.Coach.F_name + " " + b.TrainingSession.Coach.L_name).Trim()
                    },
                    session = new
                    {
                        sessionId = b.TrainingSession.SessionID,
                        sessionDate = b.TrainingSession.SessionDate,
                        startTime = b.TrainingSession.Start_Time,
                        endTime = b.TrainingSession.End_Time,
                        location = b.TrainingSession.Location,
                        sportName = b.TrainingSession.Sport.Name,
                        sessionType = b.TrainingSession.SessionType
                    }
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                bookings
            });
        }

        // Get pending certificates (coaches waiting for certificate verification)
        [HttpGet("certificates/pending")]
        public async Task<IActionResult> GetPendingCertificates()
        {
            var pendingCertificates = await _dbContext.Coaches
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
                })
                .ToListAsync();

            return Ok(pendingCertificates);
        }

        // Verify certificate (same as verify coach, but focused on certificate)
        [HttpPut("certificates/{coachId}/verify")]
        public async Task<IActionResult> VerifyCertificate(int coachId, [FromBody] string? notes = null)
        {
            var coach = await _dbContext.Coaches
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CoachID == coachId);

            if (coach == null)
            {
                return NotFound(new { error = "Coach not found" });
            }

            if (string.IsNullOrEmpty(coach.CertificateUrl))
            {
                return BadRequest(new { error = "No certificate uploaded" });
            }

            // Get admin ID
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(adminIdClaim, out int adminId))
            {
                return Unauthorized();
            }

            coach.VerificationStatus = VerificationStatus.Approved;
            coach.VerifiedAt = DateTime.UtcNow;
            coach.VerifiedByAdminId = adminId;
            coach.VerificationNotes = notes;

            // Add Coach role
            var user = coach.User;
            var hasCoachRole = await _userManager.IsInRoleAsync(user, "Coach");
            if (!hasCoachRole)
            {
                await _userManager.AddToRoleAsync(user, "Coach");
            }

            // Update primary user type
            if (user.PrimaryUserType != UserType.Coach)
            {
                user.PrimaryUserType = UserType.Coach;
                await _userManager.UpdateAsync(user);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Certificate verified successfully" });
        }

        // Get reviews pending moderation (flagged reviews or all recent reviews)
        [HttpGet("reviews/pending")]
        public async Task<IActionResult> GetPendingReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            // For now, return recent reviews (you can add a "IsFlagged" field later)
            var query = _dbContext.Reviews
                .Include(r => r.Client)
                .Include(r => r.Coach)
                .OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();

            var reviews = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.ReviewID,
                    r.Rating,
                    r.Comment,
                    r.CoachResponse,
                    r.CreatedAt,
                    Client = new
                    {
                        r.Client.ClientID,
                        Name = r.Client.F_name + " " + r.Client.L_name,
                        r.Client.Email
                    },
                    Coach = new
                    {
                        r.Coach.CoachID,
                        Name = r.Coach.F_name + " " + r.Coach.L_name
                    }
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount = totalCount,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                reviews = reviews
            });
        }

        // Moderate review (delete inappropriate review)
        [HttpPut("reviews/{reviewId}/moderate")]
        public async Task<IActionResult> ModerateReview(int reviewId, [FromBody] string action = "delete")
        {
            var review = await _dbContext.Reviews.FindAsync(reviewId);
            if (review == null)
            {
                return NotFound(new { error = "Review not found" });
            }

            if (action.ToLower() == "delete")
            {
                _dbContext.Reviews.Remove(review);
                await _dbContext.SaveChangesAsync();

                // Update coach average rating after deletion
                await UpdateCoachAverageRatingForAdmin(review.CoachID);

                return Ok(new { message = "Review deleted successfully" });
            }

            return BadRequest(new { error = "Invalid action" });
        }

        // Get analytics
        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
        {
            var now = DateTime.UtcNow;
            var startOfYear = new DateTime(now.Year, 1, 1);
            var startOfNextYear = startOfYear.AddYears(1);

            // Total users
            var totalUsers = await _dbContext.Users.CountAsync();

            // Total coaches
            var totalCoaches = await _dbContext.Coaches.CountAsync();
            var verifiedCoaches = await _dbContext.Coaches
                .CountAsync(c => c.VerificationStatus == VerificationStatus.Approved);
            var pendingCoaches = await _dbContext.Coaches
                .CountAsync(c => c.VerificationStatus == VerificationStatus.Pending);

            // Total clients
            var totalClients = await _dbContext.Clients.CountAsync();

            // Total bookings
            var totalBookings = await _dbContext.Bookings.CountAsync();
            var completedBookings = await _dbContext.Bookings
                .CountAsync(b => b.Status == BookingStatus.Completed);

            // Total reviews
            var totalReviews = await _dbContext.Reviews.CountAsync();

            // Average rating across all coaches
            var averageRating = await _dbContext.Coaches
                .Where(c => c.AvgRating > 0)
                .AverageAsync(c => (decimal?)c.AvgRating) ?? 0;

            // Monthly growth (new users this month)
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var newUsersThisMonth = await _dbContext.Users
                .CountAsync(u => u.CreatedAt >= startOfMonth);

            // Total payments
            var totalRevenue = await _dbContext.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            var monthlyBookingsRaw = await _dbContext.Bookings
                .Where(b => b.BookingDate >= startOfYear && b.BookingDate < startOfNextYear)
                .GroupBy(b => b.BookingDate.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var monthlyRevenueRaw = await _dbContext.Payments
                .Where(p => p.Status == PaymentStatus.Completed &&
                            p.TransactionDate >= startOfYear &&
                            p.TransactionDate < startOfNextYear)
                .GroupBy(p => p.TransactionDate.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Revenue = g.Sum(p => p.Amount)
                })
                .ToListAsync();

            var monthlyBookings = Enumerable.Range(1, 12)
                .Select(month => new
                {
                    Month = month,
                    MonthName = new DateTime(now.Year, month, 1).ToString("MMM"),
                    Count = monthlyBookingsRaw.FirstOrDefault(x => x.Month == month)?.Count ?? 0
                })
                .ToList();

            var monthlyRevenue = Enumerable.Range(1, 12)
                .Select(month => new
                {
                    Month = month,
                    MonthName = new DateTime(now.Year, month, 1).ToString("MMM"),
                    Revenue = monthlyRevenueRaw.FirstOrDefault(x => x.Month == month)?.Revenue ?? 0
                })
                .ToList();

            var bestRevenueMonth = monthlyRevenue
                .Where(x => x.Revenue > 0)
                .OrderByDescending(x => x.Revenue)
                .FirstOrDefault();

            var currentMonthRevenue = monthlyRevenue
                .FirstOrDefault(x => x.Month == now.Month)?.Revenue ?? 0;
            var previousMonthRevenue = now.Month > 1
                ? monthlyRevenue.FirstOrDefault(x => x.Month == now.Month - 1)?.Revenue ?? 0
                : 0;
            var revenueGrowthPercent = previousMonthRevenue > 0
                ? Math.Round(((currentMonthRevenue - previousMonthRevenue) / previousMonthRevenue) * 100, 1)
                : currentMonthRevenue > 0 ? 100 : 0;

            return Ok(new
            {
                Users = new
                {
                    Total = totalUsers,
                    Clients = totalClients,
                    Coaches = totalCoaches,
                    NewThisMonth = newUsersThisMonth
                },
                Coaches = new
                {
                    Total = totalCoaches,
                    Verified = verifiedCoaches,
                    Pending = pendingCoaches
                },
                Bookings = new
                {
                    Total = totalBookings,
                    Completed = completedBookings
                },
                Reviews = new
                {
                    Total = totalReviews,
                    AverageRating = Math.Round(averageRating, 2)
                },
                Revenue = new
                {
                    Total = totalRevenue,
                    Currency = "EGP"
                },
                MonthlyGrowth = new
                {
                    NewUsers = newUsersThisMonth,
                    Month = DateTime.UtcNow.ToString("MMMM yyyy")
                },
                RevenueAnalytics = new
                {
                    Year = now.Year,
                    TotalRevenue = totalRevenue,
                    TotalSessions = totalBookings,
                    BestMonth = bestRevenueMonth?.MonthName ?? "-",
                    GrowthPercent = revenueGrowthPercent,
                    MonthlyRevenue = monthlyRevenue,
                    MonthlyBookings = monthlyBookings
                }
            });
        }

        // Helper method for admin to update coach rating
        private async Task UpdateCoachAverageRatingForAdmin(int coachId)
        {
            var coach = await _dbContext.Coaches.FindAsync(coachId);
            if (coach == null) return;

            var averageRating = await _dbContext.Reviews
                .Where(r => r.CoachID == coachId)
                .AverageAsync(r => (decimal?)r.Rating);

            coach.AvgRating = averageRating ?? 0;
            await _dbContext.SaveChangesAsync();
        }

    }
}
