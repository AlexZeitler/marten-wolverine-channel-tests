using Alba;
using Marten;
using MartenWolverineChannels.IntegrationTests.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Wolverine;
using Xunit.Abstractions;
using IEvent = Marten.Events.IEvent;

namespace MartenWolverineChannels.IntegrationTests;

public record Register(
  Guid UserId,
  string Username,
  string EMail
);

public class User
{
  public Guid Id { get; set; }
  public string Username { get; set; }

  public User(string username)
  {
    Username = username;
  }

  public static User Create(
    Registered registered
  )
  {
    return new User(registered.Username);
  }
}

public class RegisterHandler
{
  public async Task Handle(
    Register register,
    IDocumentSession session,
    ILogger logger
  )
  {
    var (userId, username, eMail) = register;
    var registered = new Registered(
      username,
      DateTimeOffset.Now,
      eMail
    );
    session.Events.Append(userId, registered);
    await session.SaveChangesAsync();
    logger.LogInformation($"SaveChangesAsync finished, stream id {userId}");
  }
}

public class When_executing_an_async_command : IAsyncLifetime
{
  private readonly ITestOutputHelper _testOutputHelper;
  private IAlbaHost _host;
  private IReadOnlyList<IEvent> _events;
  private ILogger? _logger;
  private User? _user;

  public When_executing_an_async_command(
    ITestOutputHelper testOutputHelper
  )
  {
    _testOutputHelper = testOutputHelper;
  }

  public async Task InitializeAsync()
  {
    _host = await (await new TestServices()
      .GetHostBuilder(_testOutputHelper)).StartAlbaAsync();

    var bus = _host.Services.GetService<IMessageBus>();
    var listener = _host.Services.GetService<PollingMartenEventListener>();
    _logger = _host.Services.GetService<ILogger>();

    var userId = Guid.NewGuid();
    var username = $"johndoe-{userId}";
    await bus.PublishAsync(
      new Register(
        userId,
        username,
        "john@acme.inc"
      )
    );

    _logger.LogInformation("Listener should now wait");
    await listener.WaitForProjection<User>(e => e.Username == username);
    _logger.LogInformation("Listener should have found the projection (test must not fail now)");

    await using var session = _host.Services.GetService<IDocumentSession>();
    _logger.LogInformation($"Trying to fetch expected stream id {userId}");
    _events = await session.Events.FetchStreamAsync(userId);
    _user = await session.LoadAsync<User>(userId);
  }

  [Fact]
  public void should_persist_event()
  {
    _logger.LogInformation("Asserting Event");
    _events.Count.ShouldBe(1);
  }

  [Fact]
  public void should_project_user()
  {
    _logger.LogInformation("Asserting Projection");
    _user.ShouldNotBeNull();
  }


  public async Task DisposeAsync()
  {
    _logger.LogInformation("Disposing Host");
    await _host.DisposeAsync();
  }
}
