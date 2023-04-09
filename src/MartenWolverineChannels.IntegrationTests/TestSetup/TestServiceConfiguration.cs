using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Wolverine;

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

public class TestServices
{
  public IHostBuilder GetHostBuilder()
  {
    return Host.CreateDefaultBuilder()
      .ConfigureServices(services =>
      {
        services.AddMartenTestDb();
        services.AddSingleton<MartenEventListener>();
        services.AddSingleton<IConfigureMarten, MartenEventListenerConfig>();
      })
      .UseWolverine();
  }
}
