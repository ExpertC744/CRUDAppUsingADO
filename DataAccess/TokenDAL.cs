using CRUDAppUsingADO.DataAccess;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess;

public class TokenDAL
{
    private readonly DBHelper _db;

    public TokenDAL(DBHelper db) => _db = db;

    public async Task SaveRefreshTokenAsync(
        Guid userId, string token, string jwtId, DateTime expiresAt, string? createdByIp)
    {
        await _db.ExecuteNonQueryAsync("sp_SaveRefreshToken",
            DBHelper.Param("@UserId", userId),
            DBHelper.Param("@Token", token),
            DBHelper.Param("@JwtId", jwtId),
            DBHelper.Param("@ExpiresAt", expiresAt),
            DBHelper.Param("@CreatedByIp", createdByIp));
    }

    public async Task<RefreshTokenRecord?> GetRefreshTokenAsync(string token)
    {
        var table = await _db.ExecuteTableAsync("sp_GetRefreshToken",
            DBHelper.Param("@Token", token));

        if (table.Rows.Count == 0) return null;
        var row = table.Rows[0];

        return new RefreshTokenRecord
        {
            TokenId = (Guid)row["TokenId"],
            UserId = (Guid)row["UserId"],
            Token = row["Token"].ToString()!,
            JwtId = row["JwtId"].ToString()!,
            IsUsed = (bool)row["IsUsed"],
            IsRevoked = (bool)row["IsRevoked"],
            IssuedAt = (DateTime)row["IssuedAt"],
            ExpiresAt = (DateTime)row["ExpiresAt"]
        };
    }

    public async Task RevokeRefreshTokenAsync(string token, string? revokedByIp = null, string? replacedByToken = null)
    {
        await _db.ExecuteNonQueryAsync("sp_RevokeRefreshToken",
            DBHelper.Param("@Token", token),
            DBHelper.Param("@RevokedByIp", revokedByIp),
            DBHelper.Param("@ReplacedByToken", replacedByToken));
    }

    public async Task RevokeAllUserTokensAsync(Guid userId)
    {
        await _db.ExecuteNonQueryAsync("sp_RevokeAllUserTokens",
            DBHelper.Param("@UserId", userId));
    }
}

public class RefreshTokenRecord
{
    public Guid TokenId { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string JwtId { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public bool IsActive => !IsRevoked && !IsUsed && ExpiresAt > DateTime.UtcNow;
}