using Google.Apis.Util.Store;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace EmailClientPluma.Core.Services.Storaging;

internal class GoogleDataStore : IDataStore
{
    private readonly string _connectionString;

    public GoogleDataStore(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    public async Task ClearAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM GOOGLE_STORE";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync<T>(string key)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM GOOGLE_STORE WHERE KEY = $key";
        cmd.Parameters.AddWithValue("$key", key);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<T> GetAsync<T>(string key)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT VALUE FROM GOOGLE_STORE WHERE KEY = $key";
        cmd.Parameters.AddWithValue("$key", key);


        if (await cmd.ExecuteScalarAsync() is string result) return JsonConvert.DeserializeObject<T>(result)!;
        return default!;
    }

    public async Task StoreAsync<T>(string key, T value)
    {
        var json = JsonConvert.SerializeObject(value);
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO GOOGLE_STORE (KEY, VALUE) 
                                    VALUES ($key, $value)
                                    ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
                                   ";

        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", json);

        await command.ExecuteNonQueryAsync();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @" CREATE TABLE IF NOT EXISTS GOOGLE_STORE (
                                    KEY TEXT PRIMARY KEY,
                                    VALUE TEXT NOT NULL
                                   );
                                  ";
        command.ExecuteNonQuery();
    }
}