using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Interfaces;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Maranny.Infrastructure.Services
{
    public class EmailValidationService : IEmailValidationService
    {
        public async Task<bool> IsEmailValid(string email)
        {
            var result = await ValidateEmailDetailed(email);
            return result.isValid;
        }

        public Task<(bool isValid, string reason)> ValidateEmailDetailed(string email)
        {
            // 1. Check if email is null or empty
            if (string.IsNullOrWhiteSpace(email))
            {
                return Task.FromResult((false, "Email is required"));
            }

            // 2. Check basic format using MailAddress
            try
            {
                var mailAddress = new MailAddress(email);
                if (mailAddress.Address != email)
                {
                    return Task.FromResult((false, "Invalid email format"));
                }
            }
            catch
            {
                return Task.FromResult((false, "Invalid email format"));
            }

            // 3. Check for valid format using regex
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(email, emailRegex))
            {
                return Task.FromResult((false, "Invalid email format"));
            }

            // For graduation/testing builds we intentionally allow any
            // syntactically valid email domain, including temporary domains.
            return Task.FromResult((true, "Email is valid"));
        }
    }
}
