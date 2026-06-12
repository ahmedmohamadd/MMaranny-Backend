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
            var pendingCoachEntities = await _dbContext.Coaches
                .Include(c => c.User)
                .Include(c => c.CoachSports)
                .Where(c => c.VerificationStatus == VerificationStatus.Pending)
                .ToListAsync();

            var pendingCoaches = pendingCoachEntities
                .Select(c =>
                {
                    var experienceYears = c.ExperienceYears.GetValueOrDefault();
                    if (experienceYears <= 0)
                    {
                        experienceYears = c.CoachSports
                            .Select(cs => cs.ExperienceYears.GetValueOrDefault())
                            .DefaultIfEmpty(0)
                            .Max();
                    }

                    return new
                    {
                        c.CoachID,
                        c.F_name,
                        c.L_name,
                        c.Bio,
                        ExperienceYears = experienceYears,
                        c.CertificateUrl,
                        Email = c.User.Email,
                        PhoneNumber = string.IsNullOrWhiteSpace(c.User.PhoneNumber)
                            ? null
                            : c.User.PhoneNumber.Trim(),
                        CreatedAt = c.User.CreatedAt
                    };
                })
                .ToList();

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

                object? coachStats = null;
                if (user.Coach != null)
                {
                    var coachId = user.Coach.CoachID;
                    var sessionCount = await _dbContext.Bookings
                        .CountAsync(b => b.TrainingSession.CoachID == coachId &&
                                         b.Status != BookingStatus.Cancelled);
                    var rating = await _dbContext.Reviews
                        .Where(r => r.CoachID == coachId)
                        .AverageAsync(r => (decimal?)r.Rating) ?? user.Coach.AvgRating ?? 0;
                    var revenue = await _dbContext.Payments
                        .Where(p => p.Status == PaymentStatus.Completed &&
                                    p.TrainingSession.CoachID == coachId)
                        .SumAsync(p => (decimal?)p.Amount) ?? 0;
                    var primarySport = await _dbContext.CoachSports
                        .Where(cs => cs.CoachID == coachId)
                        .OrderBy(cs => cs.CoachSportID)
                        .Select(cs => cs.Sport.Name)
                        .FirstOrDefaultAsync();

                    coachStats = new
                    {
                        coachId,
                        sport = primarySport ?? "-",
                        sessions = sessionCount,
                        rating = Math.Round(rating, 1),
                        revenue
                    };
                }

                userList.Add(new
                {
                    id = user.Id,
                    email = user.Email,
                    name = user.Client != null
                        ? (user.Client.F_name + " " + user.Client.L_name).Trim()
                        : user.Coach != null
                            ? (user.Coach.F_name + " " + user.Coach.L_name).Trim()
                            : user.Admin != null
                                ? (user.Admin.F_name + " " + user.Admin.L_name).Trim()
                                : "",
                    primaryUserType = user.PrimaryUserType.ToString(),
                    roles,
                    isBlocked = user.IsBlocked,
                    emailConfirmed = user.EmailConfirmed,
                    createdAt = user.CreatedAt,
                    coachStats
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

        [HttpGet("coaches/top")]
        public async Task<IActionResult> GetTopCoachesBySessions([FromQuery] int limit = 5)
        {
            limit = Math.Clamp(limit, 1, 50);

            var coaches = await _dbContext.Coaches
                .Include(c => c.User)
                .Include(c => c.CoachSports)
                    .ThenInclude(cs => cs.Sport)
                .ToListAsync();

            var rows = new List<TopCoachStatsRow>();
            foreach (var coach in coaches)
            {
                var sessionCount = await _dbContext.Bookings
                    .CountAsync(b => b.TrainingSession.CoachID == coach.CoachID &&
                                     b.Status != BookingStatus.Cancelled);
                var rating = await _dbContext.Reviews
                    .Where(r => r.CoachID == coach.CoachID)
                    .AverageAsync(r => (decimal?)r.Rating) ?? coach.AvgRating ?? 0;
                var revenue = await _dbContext.Payments
                    .Where(p => p.Status == PaymentStatus.Completed &&
                                p.TrainingSession.CoachID == coach.CoachID)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;
                var sport = coach.CoachSports
                    .OrderBy(cs => cs.CoachSportID)
                    .Select(cs => cs.Sport.Name)
                    .FirstOrDefault();

                rows.Add(new TopCoachStatsRow
                {
                    Id = coach.UserId,
                    CoachId = coach.CoachID,
                    Email = coach.User.Email,
                    Name = (coach.F_name + " " + coach.L_name).Trim(),
                    PrimaryUserType = UserType.Coach.ToString(),
                    IsBlocked = coach.User.IsBlocked,
                    Sport = sport ?? "-",
                    Sessions = sessionCount,
                    Rating = Math.Round(rating, 1),
                    Revenue = revenue
                });
            }

            return Ok(new
            {
                coaches = rows
                    .OrderByDescending(row => row.Sessions)
                    .ThenByDescending(row => row.Rating)
                    .Take(limit)
                    .ToList()
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
                        .ThenInclude(c => c.CoachSports)
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
                        : b.TrainingSession.Coach.CoachSports
                            .Where(cs => cs.SportID == b.TrainingSession.SportID)
                            .Select(cs => cs.PricePerSession)
                            .FirstOrDefault() ?? 0,
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

        [HttpGet("marketplace")]
        public async Task<IActionResult> GetMarketplaceListings(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] string? category = null)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _dbContext.Products
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Include(p => p.Category)
                .Include(p => p.AdminProducts)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) &&
                !category.Equals("All", StringComparison.OrdinalIgnoreCase) &&
                !category.Equals("Used", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Category.CategoryName == category);
            }

            if (category?.Equals("Used", StringComparison.OrdinalIgnoreCase) == true)
            {
                query = query.Where(p => p.Condition != null && p.Condition.Contains("Used"));
            }

            var now = DateTime.UtcNow;
            var weekStart = now.AddDays(-7);
            var allProductsQuery = _dbContext.Products.AsQueryable();
            var totalCount = await query.CountAsync();

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.ProductID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    id = p.ProductID,
                    productId = p.ProductID,
                    productName = p.ProductName,
                    seller = new
                    {
                        id = p.Client.UserId,
                        clientId = p.ClientID,
                        name = (p.Client.F_name + " " + p.Client.L_name).Trim(),
                        email = p.Client.User.Email
                    },
                    sellerName = (p.Client.F_name + " " + p.Client.L_name).Trim(),
                    categoryId = p.CategoryID,
                    category = p.Category.CategoryName,
                    categoryName = p.Category.CategoryName,
                    condition = p.Condition,
                    price = p.Price,
                    createdAt = p.CreatedAt,
                    location = p.ListingLocation,
                    isFlagged = p.AdminProducts.Any()
                })
                .ToListAsync();

            var categories = await _dbContext.Categories
                .OrderBy(c => c.CategoryID)
                .Select(c => new
                {
                    id = c.CategoryID,
                    name = c.CategoryName,
                    productCount = c.Products.Count
                })
                .ToListAsync();

            var flaggedCount = await _dbContext.Products
                .CountAsync(p => p.AdminProducts.Any());
            var listedThisWeek = await allProductsQuery
                .CountAsync(p => p.CreatedAt >= weekStart);
            var activeSellers = await allProductsQuery
                .Select(p => p.Client.UserId)
                .Distinct()
                .CountAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                stats = new
                {
                    totalListings = await allProductsQuery.CountAsync(),
                    listedThisWeek,
                    flagged = flaggedCount,
                    activeSellers
                },
                categories,
                products,
                items = products
            });
        }

        [HttpPost("marketplace/{productId:int}/flag")]
        public async Task<IActionResult> FlagMarketplaceListing(int productId)
        {
            var admin = await GetCurrentAdminAsync();
            if (admin == null)
            {
                return Unauthorized(new { error = "Admin profile not found" });
            }

            var productExists = await _dbContext.Products.AnyAsync(p => p.ProductID == productId);
            if (!productExists)
            {
                return NotFound(new { error = "Product not found" });
            }

            var alreadyFlagged = await _dbContext.AdminProducts
                .AnyAsync(ap => ap.AdminID == admin.AdminID && ap.ProductID == productId);
            if (!alreadyFlagged)
            {
                _dbContext.AdminProducts.Add(new AdminProduct
                {
                    AdminID = admin.AdminID,
                    ProductID = productId
                });
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new { message = "Listing flagged", productId, isFlagged = true });
        }

        [HttpDelete("marketplace/{productId:int}/flag")]
        public async Task<IActionResult> UnflagMarketplaceListing(int productId)
        {
            var flags = await _dbContext.AdminProducts
                .Where(ap => ap.ProductID == productId)
                .ToListAsync();
            if (flags.Count == 0)
            {
                return Ok(new { message = "Listing was not flagged", productId, isFlagged = false });
            }

            _dbContext.AdminProducts.RemoveRange(flags);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Listing unflagged", productId, isFlagged = false });
        }

        [HttpDelete("marketplace/{productId:int}")]
        public async Task<IActionResult> RemoveMarketplaceListing(int productId)
        {
            var product = await _dbContext.Products
                .FirstOrDefaultAsync(p => p.ProductID == productId);
            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            var sportProducts = await _dbContext.SportProducts
                .Where(sp => sp.ProductID == productId)
                .ToListAsync();
            var adminProducts = await _dbContext.AdminProducts
                .Where(ap => ap.ProductID == productId)
                .ToListAsync();

            _dbContext.SportProducts.RemoveRange(sportProducts);
            _dbContext.AdminProducts.RemoveRange(adminProducts);
            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Listing removed", productId });
        }

        // Get pending certificates (coaches waiting for certificate verification)
        [HttpGet("certificates/pending")]
        public async Task<IActionResult> GetPendingCertificates()
        {
            var pendingCertificateEntities = await _dbContext.Coaches
                .Include(c => c.User)
                .Include(c => c.CoachSports)
                .Where(c => c.VerificationStatus == VerificationStatus.Pending &&
                            !string.IsNullOrEmpty(c.CertificateUrl))
                .ToListAsync();

            var pendingCertificates = pendingCertificateEntities
                .Select(c =>
                {
                    var experienceYears = c.ExperienceYears.GetValueOrDefault();
                    if (experienceYears <= 0)
                    {
                        experienceYears = c.CoachSports
                            .Select(cs => cs.ExperienceYears.GetValueOrDefault())
                            .DefaultIfEmpty(0)
                            .Max();
                    }

                    return new
                    {
                        c.CoachID,
                        c.F_name,
                        c.L_name,
                        c.Bio,
                        ExperienceYears = experienceYears,
                        c.CertificateUrl,
                        Email = c.User.Email,
                        PhoneNumber = string.IsNullOrWhiteSpace(c.User.PhoneNumber)
                            ? null
                            : c.User.PhoneNumber.Trim(),
                        CreatedAt = c.User.CreatedAt
                    };
                })
                .ToList();

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
            var averageRating = await _dbContext.Reviews
                .AverageAsync(r => (decimal?)r.Rating) ?? 0;
            var ratingCounts = await _dbContext.Reviews
                .GroupBy(r => r.Rating)
                .Select(g => new
                {
                    Rating = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();
            var ratingBreakdown = Enumerable.Range(1, 5)
                .Select(rating =>
                {
                    var count = ratingCounts.FirstOrDefault(r => r.Rating == rating)?.Count ?? 0;
                    return new
                    {
                        rating,
                        count,
                        percentage = totalCount == 0
                            ? 0
                            : Math.Round(count * 100m / totalCount, 1)
                    };
                })
                .OrderByDescending(r => r.rating)
                .ToList();

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
                summary = new
                {
                    totalReviews = totalCount,
                    averageRating = Math.Round(averageRating, 1),
                    ratingBreakdown
                },
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

            // Role counts are the safest source for admin dashboard totals.
            // The Clients table can include marketplace seller profiles for coaches.
            var clientRoleId = await _dbContext.Roles
                .Where(r => r.Name == "Client")
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync();
            var coachRoleId = await _dbContext.Roles
                .Where(r => r.Name == "Coach")
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync();

            // Total coaches
            var totalCoaches = coachRoleId.HasValue
                ? await _dbContext.UserRoles.CountAsync(ur => ur.RoleId == coachRoleId.Value)
                : await _dbContext.Users.CountAsync(u => u.PrimaryUserType == UserType.Coach);
            var verifiedCoaches = await _dbContext.Coaches
                .CountAsync(c => c.VerificationStatus == VerificationStatus.Approved);
            var pendingCoaches = await _dbContext.Coaches
                .CountAsync(c => c.VerificationStatus == VerificationStatus.Pending);

            // Total trainees (client accounts in the app)
            var totalClients = clientRoleId.HasValue
                ? await _dbContext.UserRoles.CountAsync(ur => ur.RoleId == clientRoleId.Value)
                : await _dbContext.Users.CountAsync(u => u.PrimaryUserType == UserType.Client);

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
            var newClientsThisMonth = clientRoleId.HasValue
                ? await _dbContext.UserRoles
                    .Where(ur => ur.RoleId == clientRoleId.Value)
                    .Join(
                        _dbContext.Users,
                        ur => ur.UserId,
                        user => user.Id,
                        (ur, user) => user)
                    .CountAsync(user => user.CreatedAt >= startOfMonth)
                : await _dbContext.Users
                    .CountAsync(u => u.PrimaryUserType == UserType.Client && u.CreatedAt >= startOfMonth);
            var newCoachesThisMonth = coachRoleId.HasValue
                ? await _dbContext.UserRoles
                    .Where(ur => ur.RoleId == coachRoleId.Value)
                    .Join(
                        _dbContext.Users,
                        ur => ur.UserId,
                        user => user.Id,
                        (ur, user) => user)
                    .CountAsync(user => user.CreatedAt >= startOfMonth)
                : await _dbContext.Users
                    .CountAsync(u => u.PrimaryUserType == UserType.Coach && u.CreatedAt >= startOfMonth);

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
                    NewThisMonth = newUsersThisMonth,
                    NewClientsThisMonth = newClientsThisMonth,
                    NewCoachesThisMonth = newCoachesThisMonth
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
                    NewClients = newClientsThisMonth,
                    NewCoaches = newCoachesThisMonth,
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

        private async Task<Admin?> GetCurrentAdminAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return null;
            }

            return await _dbContext.Admins
                .FirstOrDefaultAsync(a => a.UserId == userId);
        }

        private sealed class TopCoachStatsRow
        {
            public int Id { get; set; }
            public int CoachId { get; set; }
            public string? Email { get; set; }
            public string Name { get; set; } = string.Empty;
            public string PrimaryUserType { get; set; } = string.Empty;
            public bool IsBlocked { get; set; }
            public string Sport { get; set; } = "-";
            public int Sessions { get; set; }
            public decimal Rating { get; set; }
            public decimal Revenue { get; set; }
        }

    }
}
