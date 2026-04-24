using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Maranny.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var email = new MimeMessage();

            // From
            email.From.Add(MailboxAddress.Parse(_configuration["EmailSettings:FromEmail"]!));

            // To
            email.To.Add(MailboxAddress.Parse(toEmail));

            // Subject
            email.Subject = subject;

            // Body
            var builder = new BodyBuilder
            {
                HtmlBody = body
            };
            email.Body = builder.ToMessageBody();

            // Send email using SMTP
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _configuration["EmailSettings:SmtpServer"],
                int.Parse(_configuration["EmailSettings:Port"]!),
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                _configuration["EmailSettings:FromEmail"],
                _configuration["EmailSettings:Password"]
            );

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        public async Task SendEmailConfirmationAsync(string toEmail, string userName, string confirmationLink)
        {
            var subject = "Confirm Your Email - Maranny";

            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2c3e50;'>Welcome to Maranny, {userName}!</h2>
                        <p>Thank you for registering. Please confirm your email address by clicking the button below:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{confirmationLink}' 
                               style='background-color: #3498db; 
                                      color: white; 
                                      padding: 12px 30px; 
                                      text-decoration: none; 
                                      border-radius: 5px;
                                      display: inline-block;'>
                                Confirm Email
                            </a>
                        </div>
                        <p style='color: #7f8c8d; font-size: 12px;'>
                            If the button doesn't work, copy and paste this link into your browser:<br/>
                            <a href='{confirmationLink}'>{confirmationLink}</a>
                        </p>
                        <p style='color: #7f8c8d; font-size: 12px;'>
                            If you didn't create this account, please ignore this email.
                        </p>
                    </div>
                </body>
                </html>
            ";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendPasswordResetAsync(string toEmail, string userName, string resetCode)
        {
            var subject = "Password Reset Code - Maranny";

            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2c3e50;'>Password Reset Request</h2>
                        <p>Hello {userName},</p>
                        <p>You requested to reset your password. Use the code below to reset your password:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <div style='background-color: #ecf0f1; 
                                        padding: 20px; 
                                        font-size: 32px; 
                                        font-weight: bold; 
                                        letter-spacing: 5px;
                                        color: #2c3e50;
                                        border-radius: 5px;'>
                                {resetCode}
                            </div>
                        </div>
                        <p style='color: #e74c3c;'>
                            This code will expire in 15 minutes.
                        </p>
                        <p style='color: #7f8c8d; font-size: 12px;'>
                            If you didn't request a password reset, please ignore this email.
                        </p>
                    </div>
                </body>
                </html>
            ";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}