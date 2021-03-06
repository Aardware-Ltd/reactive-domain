using System;
using System.Collections.Generic;

namespace ReactiveDomain
{
    /// <summary>
    /// Represents a source of events from the perspective of restoring from and taking events. To be used by infrastructure code only.
    /// </summary>
    public interface IEventSource
    {
        /// <summary>
        /// Gets the unique identifier for this EventSource
        /// This must be provided by the implementing class
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets or sets the expected version this instance is at.
        /// </summary>
        long ExpectedVersion { get; set; }

        /// <summary>
        /// Restores this instance from the history of events.
        /// </summary>
        /// <param name="events">The events to restore from.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="events"/> is <c>null</c>.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when this instance has already recorded events.</exception>
        void RestoreFromEvents(IEnumerable<object> events);

        /// <summary>
        /// Restores this instance from a single event.
        /// Avoids boxing and unboxing single values.
        /// </summary>
        /// <param name="@event">The event to restore from.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="@event"/> is <c>null</c>.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when this instance has already recorded events.</exception>
        void RestoreFromEvent(object @event);

        /// <summary>
        /// Takes the recorded history of events from this instance (CQS violation, beware).
        /// </summary>
        /// <returns>The recorded events.</returns>
        object[] TakeEvents();
    }
}