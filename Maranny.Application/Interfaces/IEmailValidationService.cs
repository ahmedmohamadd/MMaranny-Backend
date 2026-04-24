using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Maranny.Application.Interfaces
{
    public interface IEmailValidationService
    {
        Task<bool> IsEmailValid(string email);
        Task<(bool isValid, string reason)> ValidateEmailDetailed(string email);
    }
}