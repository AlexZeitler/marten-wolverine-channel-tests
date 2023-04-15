using Npgsql;

namespace MartenWolverineChannels.IntegrationTests.TestSetup;

public class PostgresAdministration
{
  private readonly string _connectionString;

  public PostgresAdministration(string connectionString)
  {
    _connectionString = connectionString;
  }

  public async Task CreateDatabase(string databaseName)
  {
    await using var connection = new NpgsqlConnection
    {
      ConnectionString = _connectionString
    };
    await connection.OpenAsync();
    var command = new NpgsqlCommand(
      $"CREATE DATABASE {databaseName}",
      connection
    );
    await command.ExecuteNonQueryAsync();
    await connection.CloseAsync();
  }

  public async Task DropDatabase(string databaseName)
  {
    await using var connection = new NpgsqlConnection
    {
      ConnectionString = _connectionString
    };
    await connection.OpenAsync();
    var command = new NpgsqlCommand(
      $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE);",
      connection
    );
    await command.ExecuteNonQueryAsync();
    await connection.CloseAsync();
  }
}
