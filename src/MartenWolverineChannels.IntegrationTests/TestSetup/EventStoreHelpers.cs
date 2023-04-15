using Npgsql;

namespace MartenWolverineChannels.IntegrationTests.TestSetup;

public class EventStoreHelpers
{
  public static string GetTestDbName()
  {
    return $"test_{Guid.NewGuid().ToString().ToLower().Substring(0, 8)}";
  }

  public static string GetTestConnectionString()
  {
    var connectionStringBuilder = new NpgsqlConnectionStringBuilder()
    {
      Pooling = false,
      Port = 5435,
      Host = "localhost",
      CommandTimeout = 20,
      Database = "postgres",
      Password = "123456",
      Username = "postgres"
    };
    var pgTestConnectionString = connectionStringBuilder.ToString();

    return pgTestConnectionString;
  }

  public static string GetTestConnectionString(
    string dbName
  )
  {
    var connectionStringBuilder = new NpgsqlConnectionStringBuilder()
    {
      Pooling = false,
      Port = 5435,
      Host = "localhost",
      CommandTimeout = 20,
      Database = dbName,
      Password = "123456",
      Username = "postgres"
    };
    var pgTestConnectionString = $"{connectionStringBuilder};Include Error Detail=True";

    return pgTestConnectionString;
  }
}
