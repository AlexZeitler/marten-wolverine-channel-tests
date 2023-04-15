using Alba;
using Marten;
using MartenWolverineChannels.IntegrationTests.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine;
using IEvent = Marten.Events.IEvent;

namespace MartenWolverineChannels.IntegrationTests;

public record Register(Guid UserId, string Username, string EMail);

public class RegisterHandler
{
  public async Task Handle(Register register, IDocumentSession session)
  {
    var (userId, username, eMail) = register;
    var registered = new Registered(username, DateTimeOffset.Now, eMail);
    session.Events.Append(userId, registered);
    await session.SaveChangesAsync();
  }
}

public class When_executing_an_async_command : IAsyncLifetime
{
  private IAlbaHost _host;
  private IReadOnlyList<IEvent> _events;

  public async Task InitializeAsync()
  {
    _host = await (await new TestServices()
      .GetHostBuilder()).StartAlbaAsync();

    var bus = _host.Services.GetService<IMessageBus>();
    var listener = _host.Services.GetService<PollingMartenEventListener>();

    var userId = Guid.NewGuid();
    var username = $"johndoe-{userId}";
    await bus.PublishAsync(new Register(userId, username, "john@acme.inc"));

    await listener.WaitFor<Registered>(e => e.Username == username);

    await using var session = _host.Services.GetService<IDocumentSession>();
    _events = await session.Events.FetchStreamAsync(userId);
  }

  [Fact]
  public void should_persist_event() => _events.Count.ShouldBe(1);


  public async Task DisposeAsync() => await _host.DisposeAsync();
}
