using System.Threading.Channels;
using Marten;
using Marten.Events;
using Marten.Events.Projections;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MartenWolverineChannels.IntegrationTests.TestSetup;

public class PollingMartenEventListener : IProjection, IChangeListener
{
  private readonly ILogger _logger;

  public PollingMartenEventListener()
  {
    _logger = NullLogger.Instance;
  }

  public PollingMartenEventListener(
    ILogger logger
  )
  {
    _logger = logger;
  }
  
  readonly List<IEvent> _events = new();

  public void Apply(IDocumentOperations operations, IReadOnlyList<StreamAction> streams)
  {
    var events = streams.SelectMany(s => s.Events);
    _events.AddRange(events);
  }

  public Task ApplyAsync(IDocumentOperations operations,
    IReadOnlyList<StreamAction> streams,
    CancellationToken cancellation)
  {
    Apply(operations, streams);

    return Task.CompletedTask;
  }

  public Task AfterCommitAsync(IDocumentSession session,
    IChangeSet commit,
    CancellationToken token)
  {
    _events.AddRange(commit.GetEvents());

    return Task.CompletedTask;
  }

  public Task WaitFor<T>(Func<T, bool> predicate, CancellationToken? token = default)
  {
    void Check(CancellationToken cancel)
    {
      var from = 0;

      while (!cancel.IsCancellationRequested)
      {
        var upTo = _events.Count;

        for (var index = from; index < upTo; index++)
        {
          var ev = _events[index];

          if (typeof(T).IsAssignableFrom(ev.EventType) && predicate((T) ev.Data))
          {
            return;
          }
        }

        from = upTo;

        Thread.Sleep(200);
      }

      cancel.ThrowIfCancellationRequested();
    }

    var cts = new CancellationTokenSource();
    cts.CancelAfter(TimeSpan.FromSeconds(10));

    var t = token ?? cts.Token;

    return Task.Run(() => Check(t), t);
  }
}


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
    try
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
    catch (Exception e)
    {
      Console.WriteLine(e);
    }
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
    var listener = services.GetService<PollingMartenEventListener>();
    options.Projections.Add(listener);
    options.Projections.AsyncListeners.Add(listener);
  }
}
