using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Entities;
using Maranny.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
namespace Maranny.Infrastructure.Services
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, GetUserFullName(user)),
                new Claim("userType", user.PrimaryUserType.ToString()),
                new Claim("isBlocked", user.IsBlocked.ToString().ToLower()),
                new Claim("emailConfirmed", user.EmailConfirmed.ToString().ToLower())
            };

            // Add roles as claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]!)
            );

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    int.Parse(_configuration["JwtSettings:ExpiryMinutes"]!)
                ),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]!);

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false,  // Don't validate expiry for refresh token flow
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _configuration["JwtSettings:Issuer"],
                    ValidAudience = _configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                }, out _);

                return principal;
            }
            catch
            {
                return null;
            }
        }

        public int? GetUserIdFromExpiredToken(string token)
        {
            var principal = ValidateToken(token);
            if (principal == null) return null;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            return null;
        }

        private string GetUserFullName(ApplicationUser user)
        {
            // Try to get name from related entities
            if (user.Client != null)
                return $"{user.Client.F_name} {user.Client.L_name}";
            if (user.Coach != null)
                return $"{user.Coach.F_name} {user.Coach.L_name}";
            if (user.Admin != null)
                return $"{user.Admin.F_name} {user.Admin.L_name}";

            return user.Email ?? "User";
        }
    }
}