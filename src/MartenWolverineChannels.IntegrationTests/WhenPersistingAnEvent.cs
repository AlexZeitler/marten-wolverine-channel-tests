using Alba;
using Marten;
using MartenWolverineChannels.IntegrationTests.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
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
    _host = await (await new TestServices()
        .GetHostBuilder()).StartAlbaAsync();

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
