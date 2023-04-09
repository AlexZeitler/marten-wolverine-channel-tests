using Alba;
using JasperFx.Core;
using Marten;
using MartenWolverineChannels.IntegrationTests.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Wolverine;
using IEvent = Marten.Events.IEvent;

namespace MartenWolverineChannels.IntegrationTests;

public class Bus
{
  private readonly IDocumentStore _store;

  public Bus(IDocumentStore store)
  {
    _store = store;
  }

  public async Task Publish(Register register)
  {
    await Task.Delay(4.Seconds());
    var (userId, username, eMail) = register;
    var registered = new Registered(username, DateTimeOffset.Now, eMail);
    await using var session = _store.OpenSession();
    session.Events.Append(userId, registered);
    await session.SaveChangesAsync();
  }
}

public class When_listening_for_events : IAsyncLifetime
{
  private IAlbaHost _host;
  private IReadOnlyList<IEvent> _events;

  public async Task InitializeAsync()
  {
    _host = await Host.CreateDefaultBuilder()
      .ConfigureServices(services => services.AddMartenTestDb().AddMartenEventListener())
      .UseWolverine()
      .StartAlbaAsync();

    var streamId = Guid.NewGuid();

    var store =
      _host.Services.GetService<IDocumentStore>() ??
      throw new InvalidOperationException();
    var username = $"jane-{streamId}";
    var registered = new Register(streamId, username, "jd@acme.inc");

    var bus = new Bus(store);
    bus.Publish(registered);

    var listener = _host.Services.GetService<MartenEventListener>() ?? throw new InvalidOperationException();
    await listener.ForEvent<Registered>(e => e.Username == username);

    await using var session = store.OpenSession();
    _events = await session.Events.FetchStreamAsync(streamId);
  }

  [Fact]
  public void should_capture_them() => _events.Count.ShouldBe(1);

  public async Task DisposeAsync() => await _host.DisposeAsync();
}
