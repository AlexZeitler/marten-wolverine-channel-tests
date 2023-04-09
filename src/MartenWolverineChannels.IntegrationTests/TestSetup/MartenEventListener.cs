using System.Threading.Channels;
using Marten;
using Marten.Events;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MartenWolverineChannels.IntegrationTests.TestSetup;

public class MartenEventListener : IDocumentSessionListener
{
  private readonly ILogger _logger;

  public MartenEventListener()
  {
    _logger = NullLogger.Instance;
  }

  public MartenEventListener(
    ILogger logger
  )
  {
    _logger = logger;
  }

  private readonly Channel<IEvent> _events = Channel.CreateUnbounded<IEvent>();
  private readonly Channel<object> _updates = Channel.CreateUnbounded<object>();

  private ChannelReader<IEvent> EventsReader => _events.Reader;
  private ChannelWriter<IEvent> EventsWriter => _events.Writer;

  private ChannelReader<object> UpdatesReader => _updates.Reader;
  private ChannelWriter<object> UpdatesWriter => _updates.Writer;

  public void BeforeSaveChanges(
    IDocumentSession session
  )
  {
  }

  public Task BeforeSaveChangesAsync(
    IDocumentSession session,
    CancellationToken token
  )
    => Task.CompletedTask;

  public void DocumentLoaded(
    object id,
    object document
  )
  {
  }

  public void DocumentAddedForStorage(
    object id,
    object document
  )
  {
  }

  public void AfterCommit(
    IDocumentSession session,
    IChangeSet commit
  )
  {
    AfterCommitAsync(
        session,
        commit,
        CancellationToken.None
      )
      .ConfigureAwait(false)
      .GetAwaiter()
      .GetResult();
  }

  public async Task AfterCommitAsync(
    IDocumentSession session,
    IChangeSet commit,
    CancellationToken token
  )
  {
    foreach (var updated in commit.Updated)
    {
      _logger.LogDebug("Projection recorded: {EventType}", updated.GetType());
      await UpdatesWriter.WriteAsync(updated, token);
    }

    await RecordEvents(commit.GetEvents(), token);
  }

  async Task RecordEvents(
    IEnumerable<IEvent> events,
    CancellationToken cancellation
  )
  {
    using (_logger.BeginScope(new Dictionary<string, object>() { ["Context"] = nameof(MartenEventListener) }))
    {
      foreach (var @event in events)
      {
        _logger.LogDebug("Event recorded: {EventType}", @event.EventType);
        await EventsWriter.WriteAsync(@event, cancellation)
          .ConfigureAwait(false);
      }
    }
  }

  public async Task ForEvent<T>(
    Func<T, bool> predicate,
    CancellationToken? token = default
  )
  {
    var cts = new CancellationTokenSource();
    cts.CancelAfter(TimeSpan.FromSeconds(10));

    var t = token ?? cts.Token;

    await foreach (var ev in EventsReader.ReadAllAsync(t)
                     .ConfigureAwait(false))
    {
      if (ev.Data is not T data)
      {
        _logger.LogDebug(
          "Event ${Event} for ${Predicate} not found. Waiting...",
          ev.EventType,
          predicate.Target
        );
        continue;
      }

      if (predicate(data))
      {
        _logger.LogDebug(
          "Event ${Event} for ${Predicate} found",
          ev.EventType,
          predicate
        );
        return;
      }
    }

    throw new Exception("No events were found.");
  }

  public async Task ForAsyncProjection<T>(
    Func<T, bool> predicate,
    CancellationToken? token = default
  )
  {
    var cts = new CancellationTokenSource();
    cts.CancelAfter(TimeSpan.FromSeconds(10));

    var t = token ?? cts.Token;

    await foreach (var projection in UpdatesReader.ReadAllAsync(t)
                     .ConfigureAwait(false))
    {
      _logger.LogDebug("Projections already recorded: {Projection}", projection.GetType());
      if (projection is not T p)
      {
        continue;
      }

      if (predicate(p))
      {
        return;
      }
    }

    throw new Exception("No projection updates were found.");
  }
}

public class MartenEventListenerConfig : IConfigureMarten
{
  public void Configure(
    IServiceProvider services,
    StoreOptions options
  )
  {
    var listener = services.GetService<MartenEventListener>();
    options.Listeners.Add(listener);
    options.Projections.AsyncListeners.Add(listener);
  }
}
