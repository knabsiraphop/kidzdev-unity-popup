using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.Popup
{
    /// <summary>
    /// The default loader the <see cref="PopupManager"/> uses: holds one <see cref="IPopupLoader"/> per
    /// <see cref="PopupSource"/> and routes each call by <c>reference.Source</c>. This is what makes mixed
    /// Resources / Direct / Addressables popups "just work" — no call site cares how a popup is loaded.
    /// </summary>
    /// <remarks>
    /// A project that never uses Addressables wires only Resources + Direct (see <see cref="CreateDefault"/>)
    /// and pulls in zero extra dependency — the <c>AddressablesPopupLoader</c> lives in <c>Samples~</c>.
    /// </remarks>
    public sealed class CompositePopupLoader : IPopupLoader
    {
        private readonly Dictionary<PopupSource, IPopupLoader> _loaders =
            new Dictionary<PopupSource, IPopupLoader>();

        /// <summary>Registers (or replaces) the inner loader for <paramref name="source"/>. Returns <c>this</c> for chaining.</summary>
        public CompositePopupLoader With(PopupSource source, IPopupLoader loader)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            _loaders[source] = loader;
            return this;
        }

        /// <summary>The standard wiring for a project without Addressables: Resources + Direct.</summary>
        public static CompositePopupLoader CreateDefault()
        {
            return new CompositePopupLoader()
                .With(PopupSource.Resources, new ResourcesPopupLoader())
                .With(PopupSource.Direct, new DirectPopupLoader());
        }

        private IPopupLoader Resolve(PopupSource source)
        {
            if (_loaders.TryGetValue(source, out var loader)) return loader;
            throw new InvalidOperationException(
                $"No IPopupLoader registered for PopupSource.{source}. " +
                $"Register one via CompositePopupLoader.With(PopupSource.{source}, ...).");
        }

        /// <inheritdoc/>
        public UniTask<GameObject> LoadPrefabAsync(PopupRef reference, CancellationToken ct)
            => Resolve(reference.Source).LoadPrefabAsync(reference, ct);

        /// <inheritdoc/>
        public void ReleasePrefab(PopupRef reference, GameObject prefab)
            => Resolve(reference.Source).ReleasePrefab(reference, prefab);
    }
}
