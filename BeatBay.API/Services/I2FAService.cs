using Microsoft.AspNetCore.Identity.UI.Services;
using System.Security.Cryptography;
using System.Text;

namespace BeatBay.Services
{
    public interface I2FAService
    {
        Task<string> GenerateCodeAsync(int userId);
        Task<bool> ValidateCodeAsync(int userId, string code);
        Task SendCodeByEmailAsync(string email, string code);
    }
}