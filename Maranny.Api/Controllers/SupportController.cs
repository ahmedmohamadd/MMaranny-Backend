using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api")]
    public class SupportController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public SupportController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost("support/contact")]
        public async Task<IActionResult> ContactSupport(ContactSupportDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId.Value);
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId.Value);
            var senderEmail = string.IsNullOrWhiteSpace(dto.Email)
                ? user?.Email
                : dto.Email.Trim();
            var senderName = client != null
                ? $"{client.F_name} {client.L_name}".Trim()
                : coach != null
                    ? $"{coach.F_name} {coach.L_name}".Trim()
                    : user?.UserName ?? "Maranny user";
            var userType = GetCurrentUserType();

            var notification = new Notification
            {
                Title = $"Support request from {senderName}",
                Message =
                    $"Role: {userType}\nEmail: {senderEmail ?? "Not provided"}\n\n{dto.Message.Trim()}",
                Type = NotificationType.General,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Support request submitted successfully.",
                requestId = notification.NotificationID
            });
        }

        [HttpPost("reports")]
        public async Task<IActionResult> SubmitReport(SubmitReportDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var reporterType = GetCurrentUserType();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId.Value);
            var reportedCoach = await ResolveReportedCoach(dto);

            var report = new Report
            {
                ProductID = dto.ProductId,
                CoachID = reportedCoach?.CoachID ?? dto.CoachId,
                ReporterType = reporterType,
                ReportedType = dto.ReportedType?.Trim(),
                Reason = dto.Reason.Trim(),
                Description = BuildReportDescription(dto, reportedCoach),
                Priority = dto.Priority?.Trim() ?? "Normal",
                Status = ReportStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Reports.Add(report);
            await _dbContext.SaveChangesAsync();

            if (client != null)
            {
                _dbContext.ClientReports.Add(new ClientReport
                {
                    ClientID = client.ClientID,
                    ReportID = report.ReportID
                });
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new
            {
                message = "Report submitted successfully.",
                reportId = report.ReportID,
                status = report.Status.ToString()
            });
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var userId) ? userId : null;
        }

        private string GetCurrentUserType()
        {
            return User.FindFirst("userType")?.Value ??
                   User.FindFirst(ClaimTypes.Role)?.Value ??
                   "User";
        }

        private async Task<Coach?> ResolveReportedCoach(SubmitReportDto dto)
        {
            if (dto.CoachId is > 0)
            {
                return await _dbContext.Coaches.FindAsync(dto.CoachId.Value);
            }

            var target = dto.Target?.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                return null;
            }

            return await _dbContext.Coaches
                .Include(c => c.User)
                .FirstOrDefaultAsync(c =>
                    c.User.Email == target ||
                    (c.F_name + " " + c.L_name).Contains(target));
        }

        private static string BuildReportDescription(SubmitReportDto dto, Coach? reportedCoach)
        {
            var target = string.IsNullOrWhiteSpace(dto.Target)
                ? "Not specified"
                : dto.Target.Trim();
            var resolved = reportedCoach == null
                ? "Not resolved"
                : $"{reportedCoach.F_name} {reportedCoach.L_name} (CoachID {reportedCoach.CoachID})";
            var details = string.IsNullOrWhiteSpace(dto.Description)
                ? "No extra details provided."
                : dto.Description.Trim();

            return $"Target entered: {target}\nResolved target: {resolved}\n\n{details}";
        }
    }

    public class ContactSupportDto
    {
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [MinLength(10)]
        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;
    }

    public class SubmitReportDto
    {
        [MaxLength(200)]
        public string? Target { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? CoachId { get; set; }

        public int? ProductId { get; set; }

        [MaxLength(100)]
        public string? ReportedType { get; set; }

        [MaxLength(50)]
        public string? Priority { get; set; }
    }
}
