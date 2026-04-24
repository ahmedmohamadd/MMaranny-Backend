using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Maranny.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendEmailConfirmationAsync(string toEmail, string userName, string confirmationLink);
        Task SendPasswordResetAsync(string toEmail, string userName, string resetCode);
    }
}