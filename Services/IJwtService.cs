using CRUDAppUsingADO.Models;
using System.Security.Claims;

namespace CRUDAppUsingADO.Services;

public interface IJwtService
{
    (string Token, string JwtId, DateTime ExpiresAt) GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}