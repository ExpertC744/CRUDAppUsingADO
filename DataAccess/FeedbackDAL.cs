
using Microsoft.Data.SqlClient;
using System.Data;
using CRUDAppUsingADO.Models;

namespace CRUDAppUsingADO.DataAccess
{
    public class FeedbackDAL
    {
        private readonly string _connectionString;

        public FeedbackDAL(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection connection string is missing.");
        }


        public async Task<int> AddFeedbackAsync(
            string userId,
            string userName,
            FeedbackDto dto)
        {
            await using SqlConnection con =
                new SqlConnection(_connectionString);

            await con.OpenAsync();

            await using SqlTransaction transaction =
                (SqlTransaction)await con.BeginTransactionAsync();

            try
            {
                const string query = @"
            INSERT INTO dbo.UserFeedback
                (UserId, UserName, Rating, Description)
            VALUES
                (@UserId, @UserName, @Rating, @Description);

            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                await using SqlCommand cmd =
                    new SqlCommand(query, con, transaction);

                cmd.Parameters.Add("@UserId", SqlDbType.NVarChar, 100)
                    .Value = userId;

                cmd.Parameters.Add("@UserName", SqlDbType.NVarChar, 150)
                    .Value = userName;

                cmd.Parameters.Add("@Rating", SqlDbType.Int)
                    .Value = dto.Rating;

                cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 240)
                    .Value = dto.Description.Trim();

                object? result = await cmd.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                {
                    throw new InvalidOperationException(
                        "Feedback insert did not return a FeedbackId.");
                }

                int feedbackId = Convert.ToInt32(result);

                await transaction.CommitAsync();

                return feedbackId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

    }
}
