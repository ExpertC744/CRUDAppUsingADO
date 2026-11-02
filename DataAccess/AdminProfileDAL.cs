using CRUDAppUsingADO.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess;

public class AdminProfileDAL
{
    private readonly IConfiguration _configuration;

    public AdminProfileDAL(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SqlConnection GetConnection()
    {
        return new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
    }
    public async Task<bool> IsAdminEnabledAsync(Guid userId)
    {
        using var con = GetConnection();

        const string sql = @"
        SELECT COUNT(1)
        FROM dbo.Users u
        INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
        WHERE u.UserId = @UserId
          AND r.RoleName = 'Admin'
          AND u.IsActive = 1
          AND u.nBit = 1;";

        using var cmd = new SqlCommand(sql, con);

        cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;

        await con.OpenAsync();

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }
    public async Task<AdminProfileDto?> GetProfileAsync(Guid userId)
    {
        using var con = GetConnection();
        using var cmd = new SqlCommand("dbo.sp_Admin_GetProfile", con);

        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;

        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        bool hasProfile = reader["AdminProfileId"] != DBNull.Value;

        return new AdminProfileDto
        {
            UserId = (Guid)reader["UserId"],
            FullName = reader["FullName"]?.ToString() ?? "",
            Email = reader["Email"]?.ToString() ?? "",
            PhoneNumber = reader["PhoneNumber"]?.ToString() ?? "",
            OrganizationName = reader["OrganizationName"] == DBNull.Value
                ? "" : reader["OrganizationName"].ToString()!,
            Designation = reader["Designation"] == DBNull.Value
                ? "" : reader["Designation"].ToString()!,
            Department = reader["Department"] == DBNull.Value
                ? "" : reader["Department"].ToString()!,
            City = reader["City"] == DBNull.Value
                ? "" : reader["City"].ToString()!,
            ProfileImageUrl = reader["ProfileImageUrl"] == DBNull.Value
                ? null : reader["ProfileImageUrl"].ToString(),
            nBit = Convert.ToBoolean(reader["nBit"]),
            IsProfileCreated = hasProfile
        };
    }

    public async Task SaveProfileAsync(
        Guid userId,
        AdminProfileSaveDto dto,
        string? imageUrl)
    {
        using var con = GetConnection();
        using var cmd = new SqlCommand("dbo.sp_Admin_SaveProfile", con);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
        cmd.Parameters.Add("@OrganizationName", SqlDbType.NVarChar, 200)
            .Value = dto.OrganizationName;
        cmd.Parameters.Add("@Designation", SqlDbType.NVarChar, 150)
            .Value = dto.Designation;
        cmd.Parameters.Add("@Department", SqlDbType.NVarChar, 150)
            .Value = dto.Department;
        cmd.Parameters.Add("@City", SqlDbType.NVarChar, 100)
            .Value = dto.City;
        cmd.Parameters.Add("@ProfileImageUrl", SqlDbType.NVarChar, 500)
            .Value = (object?)imageUrl ?? DBNull.Value;

        await con.OpenAsync();
        await cmd.ExecuteScalarAsync();
    }

    public async Task<List<AdminProfileDto>> GetAdminsAsync()
    {
        var list = new List<AdminProfileDto>();

        using var con = GetConnection();
        using var cmd = new SqlCommand("dbo.sp_SuperAdmin_GetAdmins", con);

        cmd.CommandType = CommandType.StoredProcedure;

        await con.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new AdminProfileDto
            {
                UserId = (Guid)reader["UserId"],
                FullName = reader["FullName"]?.ToString() ?? "",
                Email = reader["Email"]?.ToString() ?? "",
                PhoneNumber = reader["PhoneNumber"]?.ToString() ?? "",
                OrganizationName = reader["OrganizationName"] == DBNull.Value
                    ? "" : reader["OrganizationName"].ToString()!,
                Designation = reader["Designation"] == DBNull.Value
                    ? "" : reader["Designation"].ToString()!,
                Department = reader["Department"] == DBNull.Value
                    ? "" : reader["Department"].ToString()!,
                City = reader["City"] == DBNull.Value
                    ? "" : reader["City"].ToString()!,
                ProfileImageUrl = reader["ProfileImageUrl"] == DBNull.Value
                    ? null : reader["ProfileImageUrl"].ToString(),
                nBit = Convert.ToBoolean(reader["nBit"]),
                IsProfileCreated = reader["OrganizationName"] != DBNull.Value
            });
        }

        return list;
    }

    public async Task<bool> UpdateAdminStatusAsync(Guid userId, bool enabled)
    {
        using var con = GetConnection();
        using var cmd = new SqlCommand(
            "dbo.sp_SuperAdmin_UpdateAdminStatus", con);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
        cmd.Parameters.Add("@nBit", SqlDbType.Bit).Value = enabled;

        await con.OpenAsync();

        return Convert.ToBoolean(await cmd.ExecuteScalarAsync());
    }
}