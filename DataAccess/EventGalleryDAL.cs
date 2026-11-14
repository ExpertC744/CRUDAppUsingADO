using CRUDAppUsingADO.Models.Attendance;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRUDAppUsingADO.DataAccess
{
    public sealed class EventGalleryDAL
    {
        private readonly string _connectionString;

        public EventGalleryDAL(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is missing.");
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // ==========================================
        // ADD MULTIPLE IMAGES
        // Maximum 12 images per event
        // ==========================================
        public async Task AddImagesAsync(
            int eventId,
            Guid adminUserId,
            List<EventGalleryImageDto> images)
        {
            using var con = GetConnection();

            await con.OpenAsync();

            using var transaction =
                (SqlTransaction)await con.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted);

            try
            {
                // Lock the event row so concurrent uploads
                // cannot exceed the 12-image limit.
                const string eventSql = @"
                    SELECT EventId
                    FROM dbo.AdminEvents WITH (UPDLOCK, HOLDLOCK)
                    WHERE EventId = @EventId
                      AND AdminUserId = @AdminUserId
                      AND nBit = 1
                      AND nSABit = 1;";

                using (var eventCmd =
                    new SqlCommand(eventSql, con, transaction))
                {
                    eventCmd.Parameters.Add(
                        "@EventId", SqlDbType.Int).Value = eventId;

                    eventCmd.Parameters.Add(
                        "@AdminUserId",
                        SqlDbType.UniqueIdentifier).Value = adminUserId;

                    var eventResult = await eventCmd.ExecuteScalarAsync();

                    if (eventResult == null)
                    {
                        throw new InvalidOperationException(
                            "Event not found or you do not have access to this event.");
                    }
                }

                // Count already uploaded images
                const string countSql = @"
                    SELECT COUNT(*)
                    FROM dbo.EventGalleryImages
                    WHERE EventId = @EventId
                      AND AdminUserId = @AdminUserId;";

                int existingCount;

                using (var countCmd =
                    new SqlCommand(countSql, con, transaction))
                {
                    countCmd.Parameters.Add(
                        "@EventId", SqlDbType.Int).Value = eventId;

                    countCmd.Parameters.Add(
                        "@AdminUserId",
                        SqlDbType.UniqueIdentifier).Value = adminUserId;

                    existingCount = Convert.ToInt32(
                        await countCmd.ExecuteScalarAsync());
                }

                if (existingCount + images.Count > 12)
                {
                    throw new InvalidOperationException(
                        $"Maximum 12 images are allowed per event. " +
                        $"Already uploaded: {existingCount}. " +
                        $"You can upload only {12 - existingCount} more image(s).");
                }

                // Insert image records
                const string insertSql = @"
                    INSERT INTO dbo.EventGalleryImages
                    (
                        EventId,
                        AdminUserId,
                        ImageName,
                        ImagePath
                    )
                    VALUES
                    (
                        @EventId,
                        @AdminUserId,
                        @ImageName,
                        @ImagePath
                    );";

                foreach (var image in images)
                {
                    using var insertCmd =
                        new SqlCommand(insertSql, con, transaction);

                    insertCmd.Parameters.Add(
                        "@EventId", SqlDbType.Int).Value = eventId;

                    insertCmd.Parameters.Add(
                        "@AdminUserId",
                        SqlDbType.UniqueIdentifier).Value = adminUserId;

                    insertCmd.Parameters.Add(
                        "@ImageName", SqlDbType.NVarChar, 255).Value =
                        image.ImageName;

                    insertCmd.Parameters.Add(
                        "@ImagePath", SqlDbType.NVarChar, 1000).Value =
                        image.ImagePath;

                    await insertCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ==========================================
        // GET ALL IMAGES FOR SELECTED EVENT
        // ==========================================
        public async Task<List<EventGalleryImageDto>> GetImagesAsync(
            int eventId,
            Guid adminUserId)
        {
            var images = new List<EventGalleryImageDto>();

            const string sql = @"
                SELECT
                    g.GalleryImageId,
                    g.EventId,
                    g.ImageName,
                    g.ImagePath,
                    g.CreatedAt
                FROM dbo.EventGalleryImages g
                INNER JOIN dbo.AdminEvents e
                    ON e.EventId = g.EventId
                WHERE g.EventId = @EventId
                  AND g.AdminUserId = @AdminUserId
                  AND e.AdminUserId = @AdminUserId
                  AND e.nBit = 1
                  AND e.nSABit = 1
                ORDER BY g.CreatedAt DESC;";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add(
                "@EventId", SqlDbType.Int).Value = eventId;

            cmd.Parameters.Add(
                "@AdminUserId",
                SqlDbType.UniqueIdentifier).Value = adminUserId;

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                images.Add(new EventGalleryImageDto
                {
                    GalleryImageId =
                        Convert.ToInt64(reader["GalleryImageId"]),

                    EventId =
                        Convert.ToInt32(reader["EventId"]),

                    ImageName =
                        reader["ImageName"]?.ToString() ?? "",

                    ImagePath =
                        reader["ImagePath"]?.ToString() ?? "",

                    CreatedAt =
                        Convert.ToDateTime(reader["CreatedAt"])
                });
            }

            return images;
        }
    }
}
