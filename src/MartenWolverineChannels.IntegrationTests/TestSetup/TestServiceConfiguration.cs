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

  public static IServiceCollection AddMartenEventListener(this IServiceCollection services) =>
    services
      .AddSingleton<MartenEventListener>()
      .AddSingleton<IConfigureMarten, MartenEventListenerConfig>();
}

public class TestServices
{
  public IHostBuilder GetHostBuilder() =>
    Host.CreateDefaultBuilder()
      .ConfigureServices(services => services
        .AddMartenTestDb()
        .AddMartenEventListener())
      .UseWolverine();
}
