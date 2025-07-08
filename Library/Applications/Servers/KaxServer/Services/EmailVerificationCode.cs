using System;
using System.Collections.Concurrent;
using System.Linq;

namespace KaxServer.Services
{
    public class EmailVerificationCode
    {
        // 存储验证码和过期时间
        private readonly ConcurrentDictionary<string, (string Code, DateTime ExpireTime)> _verificationCodes = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastSentTime = new();
        private const int MinResendInterval = 60; // 最小重发间隔（秒）
        
        // 生成8位数验证码
        public string GenerateCode()
        {
            Random random = new Random();
            return random.Next(10000000, 99999999).ToString();
        }

        public bool CanSendCode(string email)
        {
            if (_lastSentTime.TryGetValue(email, out DateTime lastTime))
            {
                return (DateTime.Now - lastTime).TotalSeconds >= MinResendInterval;
            }
            return true;
        }

        // 保存验证码
        public void SaveCode(string email, string code)
        {
            _verificationCodes.AddOrUpdate(
                email,
                (code, DateTime.Now.AddMinutes(3)),
                (_, _) => (code, DateTime.Now.AddMinutes(3))
            );
            _lastSentTime.AddOrUpdate(
                email,
                DateTime.Now,
                (_, _) => DateTime.Now
            );
        }

        // 验证码是否有效
        public bool ValidateCode(string email, string code)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
            {
                return false;
            }

            if (_verificationCodes.TryGetValue(email, out var codeInfo))
            {
                if (DateTime.Now <= codeInfo.ExpireTime && codeInfo.Code == code)
                {
                    _verificationCodes.TryRemove(email, out _);
                    _lastSentTime.TryRemove(email, out _);
                    return true;
                }
            }
            return false;
        }

        // 清理过期验证码
        public void CleanupExpiredCodes()
        {
            var expiredEmails = _verificationCodes
                .Where(kvp => DateTime.Now > kvp.Value.ExpireTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var email in expiredEmails)
            {
                _verificationCodes.TryRemove(email, out _);
                _lastSentTime.TryRemove(email, out _);
            }
        }
    }
}