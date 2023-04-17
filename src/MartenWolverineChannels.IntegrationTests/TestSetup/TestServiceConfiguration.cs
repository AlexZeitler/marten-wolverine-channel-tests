using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Serilog;
using Serilog.Extensions.Logging;
using Wolverine;
using Xunit.Abstractions;
using static MartenWolverineChannels.IntegrationTests.TestSetup.EventStoreHelpers;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MartenWolverineChannels.IntegrationTests.TestSetup;

public static class TestServiceConfiguration
{
  public static IServiceCollection AddMartenTestDb(
    this IServiceCollection services,
    string connectionString
  )
  {
    services.AddMarten(options =>
    {
      options.Connection(connectionString);
      options.Projections.SelfAggregate<User>(ProjectionLifecycle.Inline);
    });
    return services;
  }

  public static IServiceCollection AddMartenEventListener(
    this IServiceCollection services
  ) =>
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

  public async Task<IHostBuilder> GetHostBuilder(
    ITestOutputHelper? testOutputHelper = null
  )
  {
    await _pgAdmin.CreateDatabase(_dbName);
    var testConnectionString = GetTestConnectionString(_dbName);

    if (testOutputHelper is not null)
    {
      Log.Logger = new LoggerConfiguration()
        .WriteTo.TestOutput(testOutputHelper)
        .CreateLogger();

      var serilogLogger = Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console()
        .WriteTo.TestOutput(testOutputHelper)
        .CreateLogger();

      var dotnetILogger = new SerilogLoggerFactory(serilogLogger)
        .CreateLogger<Program>();

      return Host.CreateDefaultBuilder()
        .ConfigureServices(
          services =>
          {
            services.AddSingleton<ILogger>(dotnetILogger);
            services
              .AddMartenTestDb(testConnectionString)
              .AddMartenEventListener();
          }
        )
        .UseWolverine()
        .UseSerilog(serilogLogger);
    }

    return Host.CreateDefaultBuilder()
      .ConfigureServices(
        services => services
          .AddMartenTestDb(testConnectionString)
          .AddMartenEventListener()
      )
      .UseWolverine();
  }
}
