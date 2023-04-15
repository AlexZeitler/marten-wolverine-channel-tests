using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using static MartenWolverineChannels.IntegrationTests.TestSetup.EventStoreHelpers;

namespace MartenWolverineChannels.IntegrationTests.TestSetup;

public static class TestServiceConfiguration
{
  public static IServiceCollection AddMartenTestDb(this IServiceCollection services, string connectionString)
  {
    services.AddMarten(connectionString);
    return services;
  }

  public static IServiceCollection AddMartenEventListener(this IServiceCollection services) =>
    services
      .AddSingleton<PollingMartenEventListener>()
      .AddSingleton<IConfigureMarten, MartenEventListenerConfig>();
}

public class TestServices
{
  private readonly string _dbName;
  private readonly PostgresAdministration _pgAdmin;

  public TestServices()
  {
    _dbName = GetTestDbName();
    _pgAdmin = new PostgresAdministration(GetTestConnectionString());
  }
  
  public async Task<IHostBuilder> GetHostBuilder()
  {
    await _pgAdmin.CreateDatabase(_dbName);
    var testConnectionString = GetTestConnectionString(_dbName);
    
    return Host.CreateDefaultBuilder()
      .ConfigureServices(services => services
        .AddMartenTestDb(testConnectionString)
        .AddMartenEventListener())
      .UseWolverine();
  }
}
