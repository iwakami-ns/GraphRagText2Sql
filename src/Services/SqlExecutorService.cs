using Npgsql;
using System.Data;
using System.Text.Json;

namespace GraphRagText2Sql.Services
{
    public sealed class SqlExecutorService
    {
        private readonly string _conn;
        public SqlExecutorService(string connectionString) => _conn = connectionString;

        public async Task<object[]> ExecuteAsync(string sql)
        {
            await using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var val = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = val;
                }
                rows.Add(row);
            }
            return rows.Cast<object>().ToArray();
        }
    }
}