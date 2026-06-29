using System.Collections.Generic;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// Optional id → <see cref="PopupRef"/> map so call sites say <c>ShowAsync&lt;bool&gt;(PopupId.Confirm)</c>
    /// without repeating source/location everywhere. Ids are plain objects (an enum, a string, anything with
    /// stable equality). <c>ShowAsync</c> also accepts a raw <see cref="PopupRef"/> when you don't want a registry.
    /// </summary>
    public sealed class PopupRegistry
    {
        private readonly Dictionary<object, PopupRef> _map = new Dictionary<object, PopupRef>();

        /// <summary>Maps <paramref name="id"/> to <paramref name="reference"/>. Returns <c>this</c> for fluent chaining.</summary>
        public PopupRegistry Map(object id, PopupRef reference)
        {
            _map[id] = reference;
            return this;
        }

        /// <summary>Tries to resolve <paramref name="id"/> to its registered ref.</summary>
        public bool TryGet(object id, out PopupRef reference) => _map.TryGetValue(id, out reference);

        /// <summary>Resolves <paramref name="id"/>, throwing a clear error when it isn't registered.</summary>
        public PopupRef Get(object id)
        {
            if (_map.TryGetValue(id, out var reference)) return reference;
            throw new KeyNotFoundException(
                $"No PopupRef registered for id '{id}'. Register it via PopupRegistry.Map(id, PopupRef...).");
        }
    }
}
