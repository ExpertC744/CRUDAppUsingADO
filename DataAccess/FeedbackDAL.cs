
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
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection connection string is missing.");
        }

        public async Task<int> AddFeedbackAsync(string userId, string userName, FeedbackDto dto)
        {
            await using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            await using SqlTransaction transaction = (SqlTransaction)await con.BeginTransactionAsync();

            try
            {
                const string query = @"INSERT INTO dbo.UserFeedback(UserId, UserName, Rating, Description, nBit)
            VALUES(@UserId, @UserName, @Rating, @Description, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                await using SqlCommand cmd = new SqlCommand(query, con, transaction);
                cmd.Parameters.Add("@UserId", SqlDbType.NVarChar, 100).Value = userId;
                cmd.Parameters.Add("@UserName", SqlDbType.NVarChar, 150).Value = userName;
                cmd.Parameters.Add("@Rating", SqlDbType.Int).Value = dto.Rating;
                cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 240).Value = dto.Description.Trim();
                object? result = await cmd.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                {
                    throw new InvalidOperationException("Feedback insert did not return a FeedbackId.");
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
        // GET SINGLE FEEDBACK BY ID
        public async Task<FeedbackViewModel?> GetFeedbackByIdAsync(int feedbackId)
        {
            await using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            const string query = @"
        SELECT FeedbackId, UserId, UserName, Rating, Description, nBit
        FROM dbo.UserFeedback
        WHERE FeedbackId = @FeedbackId;";

            await using SqlCommand cmd = new SqlCommand(query, con);

            cmd.Parameters.Add("@FeedbackId", SqlDbType.Int).Value = feedbackId;

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new FeedbackViewModel
                {
                    FeedbackId = reader.GetInt32(reader.GetOrdinal("FeedbackId")),
                    UserId = reader["UserId"]?.ToString() ?? string.Empty,
                    UserName = reader["UserName"]?.ToString() ?? string.Empty,
                    Rating = reader.GetInt32(reader.GetOrdinal("Rating")),
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    nBit = Convert.ToBoolean(reader["nBit"])
                };
            }

            return null;
        }


        // GET ALL FEEDBACK
        public async Task<List<FeedbackViewModel>> GetAllFeedbackAsync()
        {
            List<FeedbackViewModel> feedbackList = new List<FeedbackViewModel>();

            await using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            const string query = @"
        SELECT FeedbackId, UserId, UserName, Rating, Description, nBit
        FROM dbo.UserFeedback
        ORDER BY FeedbackId DESC;";

            await using SqlCommand cmd = new SqlCommand(query, con);

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                feedbackList.Add(new FeedbackViewModel
                {
                    FeedbackId = reader.GetInt32(reader.GetOrdinal("FeedbackId")),
                    UserId = reader["UserId"]?.ToString() ?? string.Empty,
                    UserName = reader["UserName"]?.ToString() ?? string.Empty,
                    Rating = reader.GetInt32(reader.GetOrdinal("Rating")),
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    nBit = Convert.ToBoolean(reader["nBit"])
                });
            }

            return feedbackList;
        }




        // home page
        // Get the latest approved feedback submitted by the admin
        public async Task<FeedbackViewModel?> GetLatestAdminFeedbackAsync(
            string adminUserId)
        {
            await using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            const string query = @"
        SELECT TOP (1)
            FeedbackId, UserId, UserName, Rating, Description, nBit
        FROM dbo.UserFeedback
        WHERE UserId = @AdminUserId
          AND nBit = 1
        ORDER BY FeedbackId DESC;";

            await using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.Add("@AdminUserId", SqlDbType.NVarChar, 100).Value =
                adminUserId;

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new FeedbackViewModel
            {
                FeedbackId = Convert.ToInt32(reader["FeedbackId"]),
                UserId = reader["UserId"]?.ToString() ?? string.Empty,
                UserName = reader["UserName"]?.ToString() ?? string.Empty,
                Rating = Convert.ToInt32(reader["Rating"]),
                Description = reader["Description"]?.ToString() ?? string.Empty,
                nBit = Convert.ToBoolean(reader["nBit"])
            };
        }


        // Get approved feedback submitted by other users
        public async Task<List<FeedbackViewModel>> GetOtherFeedbackAsync(
            string adminUserId)
        {
            var list = new List<FeedbackViewModel>();

            await using SqlConnection con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            const string query = @"
        SELECT FeedbackId, UserId, UserName, Rating, Description, nBit
        FROM dbo.UserFeedback
        WHERE nBit = 1
          AND UserId <> @AdminUserId
        ORDER BY FeedbackId DESC;";

            await using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.Add("@AdminUserId", SqlDbType.NVarChar, 100).Value =
                adminUserId;

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new FeedbackViewModel
                {
                    FeedbackId = Convert.ToInt32(reader["FeedbackId"]),
                    UserId = reader["UserId"]?.ToString() ?? string.Empty,
                    UserName = reader["UserName"]?.ToString() ?? string.Empty,
                    Rating = Convert.ToInt32(reader["Rating"]),
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    nBit = Convert.ToBoolean(reader["nBit"])
                });
            }

            return list;
        }
    }
}
