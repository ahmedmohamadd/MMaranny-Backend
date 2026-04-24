using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Application.Interfaces;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Maranny.Infrastructure.Services
{
    public class EmailValidationService : IEmailValidationService
    {
        private static readonly string[] DisposableEmailDomains = new[]
        {
            "tempmail.com", "throwaway.email", "guerrillamail.com", "mailinator.com",
            "10minutemail.com", "trashmail.com", "fakeinbox.com", "yopmail.com"
        };

        public async Task<bool> IsEmailValid(string email)
        {
            var result = await ValidateEmailDetailed(email);
            return result.isValid;
        }

        public async Task<(bool isValid, string reason)> ValidateEmailDetailed(string email)
        {
            // 1. Check if email is null or empty
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Email is required");
            }

            // 2. Check basic format using MailAddress
            try
            {
                var mailAddress = new MailAddress(email);
                if (mailAddress.Address != email)
                {
                    return (false, "Invalid email format");
                }
            }
            catch
            {
                return (false, "Invalid email format");
            }

            // 3. Check for valid format using regex
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(email, emailRegex))
            {
                return (false, "Invalid email format");
            }

            // 4. Check if domain is disposable
            var domain = email.Split('@')[1].ToLower();
            if (DisposableEmailDomains.Contains(domain))
            {
                return (false, "Disposable email addresses are not allowed");
            }

            // 5. Check if domain has MX records (email server exists)
            try
            {
                var domainExists = await CheckDomainMxRecords(domain);
                if (!domainExists)
                {
                    return (false, "Email domain does not exist or cannot receive emails");
                }
            }
            catch
            {
                // If MX check fails, allow it (maybe network issue)
                // Don't block user registration for network problems
            }

            return (true, "Email is valid");
        }

        private async Task<bool> CheckDomainMxRecords(string domain)
        {
            try
            {
                // Use DNS lookup to check MX records
                var hostEntry = await System.Net.Dns.GetHostEntryAsync(domain);
                return hostEntry != null;
            }
            catch
            {
                // Domain might not exist or DNS lookup failed
                return false;
            }
        }
    }
}