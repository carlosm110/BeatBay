using BeatBay.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace BeatBay.Services
{
    public class TwoFactorAuthService : I2FAService
    {
        private readonly IMemoryCache _cache;
        private readonly IEmailSender _emailSender;
        private readonly TimeSpan _codeExpiration = TimeSpan.FromMinutes(5);

        public TwoFactorAuthService(IMemoryCache cache, IEmailSender emailSender)
        {
            _cache = cache;
            _emailSender = emailSender;
        }

        public async Task<string> GenerateCodeAsync(int userId)
        {
            // Generar código de 6 dígitos
            var code = GenerateNumericCode(6);

            // Guardar en cache con expiración
            var cacheKey = $"2fa_code_{userId}";
            _cache.Set(cacheKey, code, _codeExpiration); // Corrección: removido asteriscos

            return code;
        }

        public async Task<bool> ValidateCodeAsync(int userId, string code)
        {
            var cacheKey = $"2fa_code_{userId}";
            if (_cache.TryGetValue(cacheKey, out string storedCode))
            {
                if (storedCode == code)
                {
                    // Eliminar código después de uso
                    _cache.Remove(cacheKey);
                    return true;
                }
            }
            return false;
        }

        public async Task SendCodeByEmailAsync(string email, string code)
        {
            var emailBody = $@"
Código de Verificación - BeatBay
Tu código de verificación es: {code}
Este código expira en 5 minutos.";
            await _emailSender.SendEmailAsync(email, "Código de Verificación - BeatBay", emailBody);
        }

        private string GenerateNumericCode(int length)
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var number = BitConverter.ToUInt32(bytes, 0);
            var code = (number % (int)Math.Pow(10, length)).ToString().PadLeft(length, '0');
            return code;
        }
    }
}