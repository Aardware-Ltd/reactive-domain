﻿using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

namespace ReactiveDomain.Foundation.EventStore
{
    public class StreamListener : IListener
    {
        protected readonly string ListenerName;
        private InMemoryBus _bus;
        IDisposable _subscription;
        private bool _started;
        private readonly IStreamNameBuilder _streamNameBuilder;
        private readonly object _startlock = new object();
        private readonly ManualResetEventSlim _liveLock = new ManualResetEventSlim();
        public ISubscriber EventStream => _bus;
        private readonly IStreamStoreConnection _eventStoreConnection;

        /// <summary>
        /// For listening to generic streams 
        /// </summary>
        /// <param name="listenerName"></param>
        /// <param name="eventStoreConnection">The event store to subscribe to</param>
        /// <param name="streamNameBuilder">The source for correct stream names based on aggregates and events</param>
        /// <param name="busName">The name to use for the internal bus (helpful in debugging)</param>
        public StreamListener(
                string listenerName, 
                IStreamStoreConnection eventStoreConnection, 
                IStreamNameBuilder streamNameBuilder, 
                string busName = null)
        {
            _bus = new InMemoryBus(busName ?? "Stream Listener");
            _eventStoreConnection = eventStoreConnection ?? throw new ArgumentNullException(nameof(eventStoreConnection));

            ListenerName = listenerName;
            _streamNameBuilder = streamNameBuilder;
        }
        /// <summary>
        /// Category Stream Listener
        /// i.e. $ce-[AggregateType]
        /// </summary>
        /// <typeparam name="TAggregate"></typeparam>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        public void Start<TAggregate>(int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource
        {
            Start(_streamNameBuilder.GenerateForCategory(typeof(TAggregate)), checkpoint, blockUntilLive, millisecondsTimeout);
        }
        /// <summary>
        /// Aggregate Stream listener
        /// i.e. [AggregateType]-[id]
        /// </summary>
        /// <typeparam name="TAggregate"></typeparam>
        /// <param name="id"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        public void Start<TAggregate>(Guid id, int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource
        {
            Start(_streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id), checkpoint, blockUntilLive, millisecondsTimeout);
        }

        /// <summary>
        /// Custom Stream name
        /// i.e. [StreamName]
        /// </summary>
        /// <param name="streamName"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        public virtual void Start(string streamName, int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000)
        {
            _liveLock.Reset();
            lock (_startlock)
            {
                if (_started)
                    throw new InvalidOperationException("Listener already started.");
                if (!ValidateStreamName(streamName))
                    throw new ArgumentException("Stream not found.", streamName);

                _subscription =
                    SubscribeToStreamFrom(
                        streamName,
                        checkpoint ?? 0,// StreamCheckpoint.StreamStart,
                        true,
                        eventAppeared: GotEvent,
                        liveProcessingStarted: () =>
                        {
                            _bus.Publish(new EventStoreMsg.CatchupSubscriptionBecameLive());
                            _liveLock.Set();
                        });
                _started = true;
            }
            if (blockUntilLive)
                _liveLock.Wait(millisecondsTimeout);
        }
        public IDisposable SubscribeToStreamFrom(
            string stream,
            int? lastCheckpoint,
            bool resolveLinkTos,
            Action<Message> eventAppeared,
            Action liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            int readBatchSize = 500)
        {
            var settings = new CatchUpSubscriptionSettings(10, readBatchSize, false, true);
            var sub = _eventStoreConnection.SubscribeToStreamFrom(
                stream,
                lastCheckpoint,
                settings,
                (subscription, resolvedEvent) =>  eventAppeared(resolvedEvent.DeserializeEvent() as Message),
                _ => liveProcessingStarted?.Invoke(),
                (subscription, reason, exception) => subscriptionDropped?.Invoke(reason, exception),
                userCredentials);

            return new SubscriptionDisposer(() => { sub.Stop(); return Unit.Default; });
        }

        public bool ValidateStreamName(string streamName)
        {
            return _eventStoreConnection.ReadStreamEventsForwardAsync(streamName, 0, 1, false).Result != null;
        }
        protected virtual void GotEvent(Message @event)
        {
            if (@event != null) _bus.Publish(@event);
        }
        #region Implementation of IDisposable

        private bool _disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _subscription?.Dispose();
            _bus?.Dispose();
            _disposed = true;
        }
        #endregion
    }
}
