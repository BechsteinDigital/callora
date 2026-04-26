using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Callora.Host.Backend.Application.Policies;
using Microsoft.IdentityModel.Tokens;

namespace Callora.Host.Backend.Infrastructure.Security;

public static class BackendJwtTokenIssuer
{
    public static string Issue(
        BackendHostOptions options,
        string subject,
        string? displayName,
        string? email,
        IReadOnlyCollection<string> roles,
        IReadOnlyDictionary<string, string>? customClaims = null,
        TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(roles);

        var claims = new List<Claim>
        {
            new("sub", subject)
        };

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, displayName));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        foreach (var role in roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        if (customClaims is not null)
        {
            foreach (var (key, value) in customClaims)
            {
                if (!string.IsNullOrWhiteSpace(key) && value is not null)
                {
                    claims.Add(new Claim(key, value));
                }
            }
        }

        var nowUtc = DateTime.UtcNow;
        var expiresUtc = nowUtc.Add(lifetime ?? TimeSpan.FromHours(1));
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey));

        var token = new JwtSecurityToken(
            issuer: options.JwtIssuer,
            audience: options.JwtAudience,
            claims: claims,
            notBefore: nowUtc,
            expires: expiresUtc,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
