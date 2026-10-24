using System.Data;
using Microsoft.Data.SqlClient;

namespace CRUDAppUsingADO.DataAccess;
    public class DBHelper
    {
        private readonly string _connectionString;

        public DBHelper(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        private SqlConnection GetConnection() => new(_connectionString);

        /// <summary>
        /// Executes a stored procedure and returns a populated DataTable.
        /// Use for SELECT queries that return rows.
        /// </summary>
        public DataTable ExecuteTable(string procedureName, params SqlParameter[] parameters)
        {
            using var conn = GetConnection();
            using var cmd = new SqlCommand(procedureName, conn) { CommandType = CommandType.StoredProcedure };
            if (parameters.Length > 0) cmd.Parameters.AddRange(parameters);

            var table = new DataTable();
            conn.Open();
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(table);
            return table;
        }

        /// <summary>
        /// Async version — preferred for all new code.
        /// </summary>
        public async Task<DataTable> ExecuteTableAsync(string procedureName, params SqlParameter[] parameters)
        {
            using var conn = GetConnection();
            using var cmd = new SqlCommand(procedureName, conn) { CommandType = CommandType.StoredProcedure };
            if (parameters.Length > 0) cmd.Parameters.AddRange(parameters);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            var table = new DataTable();
            table.Load(reader);
            return table;
        }

        /// <summary>
        /// Executes a stored procedure with no result set expected (INSERT/UPDATE/DELETE).
        /// Returns number of rows affected.
        /// </summary>
        public async Task<int> ExecuteNonQueryAsync(string procedureName, params SqlParameter[] parameters)
        {
            using var conn = GetConnection();
            using var cmd = new SqlCommand(procedureName, conn) { CommandType = CommandType.StoredProcedure };
            if (parameters.Length > 0) cmd.Parameters.AddRange(parameters);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Executes a stored procedure and returns a single scalar value.
        /// </summary>
        public async Task<object?> ExecuteScalarAsync(string procedureName, params SqlParameter[] parameters)
        {
            using var conn = GetConnection();
            using var cmd = new SqlCommand(procedureName, conn) { CommandType = CommandType.StoredProcedure };
            if (parameters.Length > 0) cmd.Parameters.AddRange(parameters);

            await conn.OpenAsync();
            return await cmd.ExecuteScalarAsync();
        }

        public static SqlParameter Param(string name, object? value) =>
            new(name, value ?? DBNull.Value);
    }

