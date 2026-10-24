using CRUDAppUsingADO.Models;
using CRUDAppUsingADO.DataAccess;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess;

public class UserDAL
{
    private readonly DBHelper _db;

    public UserDAL(DBHelper db) => _db = db;

    public async Task<(int ResultCode, string Message, Guid? UserId)> RegisterUserAsync(
        string fullName, string email, string? phone,
        string passwordHash, string passwordSalt, string roleName = "User")
    {
        var table = await _db.ExecuteTableAsync("sp_RegisterUser",
            DBHelper.Param("@FullName", fullName),
            DBHelper.Param("@Email", email),
            DBHelper.Param("@PhoneNumber", phone),
            DBHelper.Param("@PasswordHash", passwordHash),
            DBHelper.Param("@PasswordSalt", passwordSalt),
            DBHelper.Param("@RoleName", roleName));

        if (table.Rows.Count == 0) return (-99, "Unknown error.", null);

        var row = table.Rows[0];
        var resultCode = Convert.ToInt32(row["ResultCode"]);
        var message = row["Message"].ToString()!;
        Guid? userId = row["UserId"] == DBNull.Value ? null : (Guid)row["UserId"];

        return (resultCode, message, userId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        var table = await _db.ExecuteTableAsync("sp_GetUserByEmail",
            DBHelper.Param("@Email", email));

        if (table.Rows.Count == 0) return null;
        return MapUser(table.Rows[0]);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        var table = await _db.ExecuteTableAsync("sp_GetUserById",
            DBHelper.Param("@UserId", userId));

        if (table.Rows.Count == 0) return null;
        var row = table.Rows[0];
        return new User
        {
            UserId = (Guid)row["UserId"],
            FullName = row["FullName"].ToString()!,
            Email = row["Email"].ToString()!,
            PhoneNumber = row["PhoneNumber"] as string,
            RoleName = row["RoleName"].ToString()!,
            IsActive = (bool)row["IsActive"],
            IsEmailVerified = (bool)row["IsEmailVerified"],
            ProfilePicUrl = row["ProfilePicUrl"] as string,
            CreatedAt = (DateTime)row["CreatedAt"]
        };
    }

    public async Task<(bool IsNewUser, Guid UserId)> GetOrCreateGoogleUserAsync(
        string googleId, string email, string fullName, string? profilePicUrl)
    {
        var table = await _db.ExecuteTableAsync("sp_GetOrCreateGoogleUser",
            DBHelper.Param("@GoogleId", googleId),
            DBHelper.Param("@Email", email),
            DBHelper.Param("@FullName", fullName),
            DBHelper.Param("@ProfilePicUrl", profilePicUrl));

        var row = table.Rows[0];
        return ((bool)Convert.ToBoolean(row["IsNewUser"]), (Guid)row["UserId"]);
    }

    public async Task UpdateFailedLoginCountAsync(string email, bool reset = false)
    {
        await _db.ExecuteNonQueryAsync("sp_UpdateFailedLoginCount",
            DBHelper.Param("@Email", email),
            DBHelper.Param("@Reset", reset));
    }

    public async Task SavePasswordResetTokenAsync(Guid userId, string token, DateTime expiresAt)
    {
        await _db.ExecuteNonQueryAsync("sp_SavePasswordResetToken",
            DBHelper.Param("@UserId", userId),
            DBHelper.Param("@Token", token),
            DBHelper.Param("@ExpiresAt", expiresAt));
    }

    public async Task<(Guid? UserId, DateTime? ExpiresAt, bool IsUsed)> ValidatePasswordResetTokenAsync(string token)
    {
        var table = await _db.ExecuteTableAsync("sp_ValidatePasswordResetToken",
            DBHelper.Param("@Token", token));

        if (table.Rows.Count == 0) return (null, null, false);
        var row = table.Rows[0];
        return ((Guid)row["UserId"], (DateTime)row["ExpiresAt"], (bool)row["IsUsed"]);
    }

    public async Task ResetPasswordAsync(Guid userId, string passwordHash, string passwordSalt, string token)
    {
        await _db.ExecuteNonQueryAsync("sp_ResetPassword",
            DBHelper.Param("@UserId", userId),
            DBHelper.Param("@PasswordHash", passwordHash),
            DBHelper.Param("@PasswordSalt", passwordSalt),
            DBHelper.Param("@Token", token));
    }

    public async Task InsertAuditLogAsync(
        Guid? userId, string? email, string action,
        string? ipAddress, string? userAgent, bool success, string? details = null)
    {
        await _db.ExecuteNonQueryAsync("sp_InsertAuditLog",
            DBHelper.Param("@UserId", userId),
            DBHelper.Param("@Email", email),
            DBHelper.Param("@Action", action),
            DBHelper.Param("@IpAddress", ipAddress),
            DBHelper.Param("@UserAgent", userAgent),
            DBHelper.Param("@Success", success),
            DBHelper.Param("@Details", details));
    }

    private static User MapUser(DataRow row) => new()
    {
        UserId = (Guid)row["UserId"],
        FullName = row["FullName"].ToString()!,
        Email = row["Email"].ToString()!,
        PhoneNumber = row["PhoneNumber"] as string,
        PasswordHash = row["PasswordHash"] as string,
        PasswordSalt = row["PasswordSalt"] as string,
        IsActive = (bool)row["IsActive"],
        IsEmailVerified = (bool)row["IsEmailVerified"],
        FailedLoginCount = (int)row["FailedLoginCount"],
        LockoutUntil = row["LockoutUntil"] == DBNull.Value ? null : (DateTime)row["LockoutUntil"],
        GoogleId = row["GoogleId"] as string,
        ProfilePicUrl = row["ProfilePicUrl"] as string,
        RoleName = row["RoleName"].ToString()!
    };
}