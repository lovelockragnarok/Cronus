using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elders.Cronus.DomainModeling;
using System.Collections.Concurrent;

namespace Elders.Cronus.EventSourcing.InMemory
{
    public class AggregateVersionService
    {
        ConcurrentDictionary<IAggregateRootId, int> reservedRevisions = new ConcurrentDictionary<IAggregateRootId, int>();

        public int ReserveVersion(IAggregateRootId aggregateId, int requestedVersion)
        {
            var reservedVersion = reservedRevisions.AddOrUpdate(aggregateId, requestedVersion, (ar, current) => ++current);
            if (requestedVersion == reservedVersion)
                return reservedVersion;
            else
                return reservedVersion - 1;
        }
    }

    public class InMemoryEventStore : IAggregateRepository, IEventStorePersister, IEventStorePlayer, IEventStoreStorageManager
    {
        private static AggregateVersionService versionService = new AggregateVersionService();
        private static ConcurrentDictionary<IAggregateRootId, ConcurrentQueue<EventsStream>> events;
        private static ConcurrentQueue<IEvent> eventsForReplay = new ConcurrentQueue<IEvent>();

        public void Save<AR>(AR aggregateRoot) where AR : IAggregateRoot
        {
            if (aggregateRoot.UncommittedEvents == null || aggregateRoot.UncommittedEvents.Count == 0)
                return;
            aggregateRoot.State.Version += 1;

            int reservedVersion = versionService.ReserveVersion(aggregateRoot.State.Id, aggregateRoot.State.Version);
            if (reservedVersion != aggregateRoot.State.Version)
            {
                throw new Exception("Retry command");
            }
            this.Persist(new List<IAggregateRoot>() { aggregateRoot });
            aggregateRoot.UncommittedEvents.Clear();
        }

        public AR Load<AR>(IAggregateRootId id) where AR : IAggregateRoot
        {
            if (!events.ContainsKey(id))
                return default(AR);
            else
            {
                var evnts = new ConcurrentQueue<IEvent>();

                var eventsStreams = events[id];

                AR aggregateRoot = AggregateRootFactory.Build<AR>(eventsStreams.SelectMany(x => x.Events).ToList());
                aggregateRoot.State.Version = eventsStreams.Last().Version;
                return aggregateRoot;
            }
        }

        public void Persist(List<IAggregateRoot> aggregates)
        {
            foreach (var aggregate in aggregates)
            {
                if (events.ContainsKey(aggregate.State.Id))
                {
                    var evnts = events[aggregate.State.Id];

                    if (evnts == null)
                        events[aggregate.State.Id] = new ConcurrentQueue<EventsStream>();

                    var eventsStream = new EventsStream();
                    eventsStream.Version = aggregate.State.Version;

                    foreach (var evnt in aggregate.UncommittedEvents)
                    {
                        eventsStream.Events.Enqueue(evnt);
                        eventsForReplay.Enqueue(evnt);
                    }

                    events[aggregate.State.Id].Enqueue(eventsStream);
                }
                else
                {
                    var evnts = new ConcurrentQueue<EventsStream>();

                    var eventsStream = new EventsStream();
                    eventsStream.Version = aggregate.State.Version;

                    foreach (var evnt in aggregate.UncommittedEvents)
                    {
                        eventsStream.Events.Enqueue(evnt);
                        eventsForReplay.Enqueue(evnt);
                    }

                    evnts.Enqueue(eventsStream);

                    events.TryAdd(aggregate.State.Id, evnts);
                }
            }
        }

        public IEnumerable<IEvent> GetEventsFromStart(int batchPerQuery = 1)
        {
            return eventsForReplay;
        }

        public void CreateEventsStorage()
        {
            events = new ConcurrentDictionary<IAggregateRootId, ConcurrentQueue<EventsStream>>();
        }

        public void CreateStorage()
        {
            this.CreateEventsStorage();
            this.CreateSnapshotsStorage();
        }

        public void CreateSnapshotsStorage()
        {

        }

        private class EventsStream
        {
            public EventsStream()
            {
                Events = new ConcurrentQueue<IEvent>();
            }

            public int Version { get; set; }

            public ConcurrentQueue<IEvent> Events { get; set; }
        }
    }
}