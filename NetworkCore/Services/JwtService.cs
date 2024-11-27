using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public string Username { get; set; } = string.Empty;
}

public class JwtService
{
    private const string KEY_FOLDER = "Config";
    private const string KEY_FILE_NAME = "jwt_key.dat";
    private const string ISSUER = "DRX-NDV";      // 添加令牌颁发者
    private const string AUDIENCE = "DRX-Client";  // 添加令牌受众
    private readonly SymmetricSecurityKey _signingKey;

    public JwtService()
    {
        var key = GetOrCreateSecretKey();
        _signingKey = new SymmetricSecurityKey(key);
    }

    private static string GetKeyFilePath()
    {
        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, KEY_FOLDER);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return Path.Combine(directory, KEY_FILE_NAME);
    }

    private byte[] GetOrCreateSecretKey()
    {
        var keyPath = GetKeyFilePath();
        try
        {
            if (File.Exists(keyPath))
            {
                var key = File.ReadAllBytes(keyPath);
                if (key.Length >= 32) // 确保密钥长度足够
                {
                    return key;
                }
            }

            // 生成新密钥
            var newKey = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(newKey);
            }

            // 保存密钥
            File.WriteAllBytes(keyPath, newKey);
            return newKey;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法创建或读取JWT密钥文件: {ex.Message}", ex);
        }
    }

    public static void ConfigureJwtAuthentication(IServiceCollection services)
    {
        var jwtService = new JwtService();
        services.AddSingleton(jwtService);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwtService._signingKey,
                    ValidateIssuer = true,
                    ValidIssuer = ISSUER,
                    ValidateAudience = true,
                    ValidAudience = AUDIENCE,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });
    }

    public string GenerateToken(UserInstance user)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.UserGuid.ToString()),
            new Claim(ClaimTypes.Role, user.UserGroup.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: ISSUER,
            audience: AUDIENCE,
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenValidationResult ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = _signingKey.Key;
            
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var username = jwtToken.Claims.First(x => x.Type == "username").Value;

            return new TokenValidationResult 
            { 
                IsValid = true, 
                Username = username 
            };
        }
        catch
        {
            return new TokenValidationResult 
            { 
                IsValid = false, 
                Username = string.Empty 
            };
        }
    }
}