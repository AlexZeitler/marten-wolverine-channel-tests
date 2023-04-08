using Alba;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Shouldly;
using Wolverine;
using Xunit.Abstractions;
using IEvent = Marten.Events.IEvent;

namespace MartenWolverineChannels.IntegrationTests;

public record Registered(string Username, DateTimeOffset On, string Email);

public class When_persisting_an_event : IAsyncLifetime
{
  private readonly ITestOutputHelper _testOutputHelper;
  private IAlbaHost _host;
  private IReadOnlyList<IEvent> _events;

  public When_persisting_an_event(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

  public async Task InitializeAsync()
  {
    var connectionString = new NpgsqlConnectionStringBuilder()
    {
      Pooling = false,
      Port = 5435,
      Host = "localhost",
      CommandTimeout = 20,
      Database = "postgres",
      Password = "123456",
      Username = "postgres"
    }.ToString();
    _host = await Host.CreateDefaultBuilder()
      .ConfigureServices(services => services.AddMarten(connectionString))
      .UseWolverine()
      .StartAlbaAsync();

    var streamId = Guid.NewGuid();
    await using var session =
      _host.Services.GetService<IDocumentSession>() ??
      throw new InvalidOperationException();
    session.Events.Append(streamId, new Registered("jane", DateTimeOffset.Now, "jd@acme.inc"));
    await session.SaveChangesAsync();

    _events = await session.Events.FetchStreamAsync(streamId);
  }

  [Fact]
  public void should_be_in_stream() => _events.Count.ShouldBe(1);

  public async Task DisposeAsync() => await _host.DisposeAsync();
}
