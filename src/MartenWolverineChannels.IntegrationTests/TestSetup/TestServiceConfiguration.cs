using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MartenWolverineChannels.IntegrationTests.TestSetup;

public static class TestServiceConfiguration
{
  public static IServiceCollection AddMartenTestDb(this IServiceCollection services)
  {
    var connectionString = new NpgsqlConnectionStringBuilder
    {
      Pooling = false,
      Port = 5435,
      Host = "localhost",
      CommandTimeout = 20,
      Database = "postgres",
      Password = "123456",
      Username = "postgres"
    }.ToString();
    services.AddMarten(connectionString);
    return services;
  }
}
