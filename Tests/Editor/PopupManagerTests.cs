using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace KidzDev.Unity.Popup.Tests
{
    [TestFixture]
    internal sealed class PopupManagerTests
    {
        private readonly List<GameObject> _prefabs = new List<GameObject>();
        private PopupManager _manager;

        [SetUp]
        public void SetUp() => FakePopup.ResetLive();

        [TearDown]
        public void TearDown()
        {
            _manager?.Dispose();
            _manager = null;
            foreach (var p in _prefabs)
                if (p != null) UnityEngine.Object.DestroyImmediate(p);
            _prefabs.Clear();
            // Reap any deferred layer/instances so they don't leak between tests.
            for (var layer = GameObject.Find("[PopupLayer]"); layer != null; layer = GameObject.Find("[PopupLayer]"))
                UnityEngine.Object.DestroyImmediate(layer);
            FakePopup.ResetLive();
        }

        private GameObject Prefab()
        {
            var p = FakePopup.CreatePrefab();
            _prefabs.Add(p);
            return p;
        }

        private FakePopup Top => FakePopup.Live[FakePopup.Live.Count - 1];

        private static readonly PopupRef AnyRef = PopupRef.Resources("x");

        [Test]
        public void ShowAsync_ReturnsValuePopupClosesWith_AndReleasesOnce()
        {
            var loader = new FakeLoader(Prefab());
            _manager = new PopupManager(loader, new GatedPopupTransition());

            var show = _manager.ShowAsync<bool>(AnyRef);
            Assert.AreEqual(1, _manager.OpenCount, "popup open while awaited");
            Top.CloseWith(true);

            Assert.IsTrue(show.GetAwaiter().GetResult());
            Assert.AreEqual(0, _manager.OpenCount, "layer empty after close");
            Assert.AreEqual(1, loader.ReleaseCount, "ReleasePrefab called exactly once per close");
        }

        [Test]
        public void ShowAsync_InstantiatesAFreshInstancePerShow()
        {
            var loader = new FakeLoader(Prefab());
            _manager = new PopupManager(loader, new GatedPopupTransition());

            var show1 = _manager.ShowAsync<bool>(AnyRef);
            var first = Top;
            first.CloseWith(true);
            show1.GetAwaiter().GetResult();

            var show2 = _manager.ShowAsync<bool>(AnyRef);
            var second = Top;
            second.CloseWith(false);
            show2.GetAwaiter().GetResult();

            Assert.AreNotSame(first, second, "each show instantiates its own copy");
            Assert.AreEqual(1, first.OpenedCount);
            Assert.AreEqual(1, second.OpenedCount);
            Assert.AreEqual(2, loader.LoadCount);
        }

        [Test]
        public void Stacking_IsLifo_ClosingTopRevealsLower()
        {
            var loader = new FakeLoader(Prefab());
            _manager = new PopupManager(loader, new GatedPopupTransition());

            var lower = _manager.ShowAsync<object>(AnyRef);
            var lowerPopup = Top;
            var upper = _manager.ShowAsync<object>(AnyRef);
            var upperPopup = Top;

            Assert.AreEqual(2, _manager.OpenCount);
            Assert.AreNotSame(lowerPopup, upperPopup);

            upperPopup.CloseWith("upper");
            Assert.AreEqual("upper", upper.GetAwaiter().GetResult());
            Assert.AreEqual(1, _manager.OpenCount, "only the top closed");

            lowerPopup.CloseWith("lower");
            Assert.AreEqual("lower", lower.GetAwaiter().GetResult());
            Assert.AreEqual(0, _manager.OpenCount);
        }

        [Test]
        public void TryHandleBack_DismissesTopOnly_AndReportsConsumed()
        {
            var loader = new FakeLoader(Prefab());
            _manager = new PopupManager(loader, new GatedPopupTransition());

            var lower = _manager.ShowAsync<object>(AnyRef);
            var lowerPopup = Top;
            var upper = _manager.ShowAsync<object>(AnyRef);

            Assert.IsTrue(_manager.TryHandleBack(), "consumed because a popup was open");
            Assert.AreEqual(PopupResult.Cancelled, upper.GetAwaiter().GetResult());
            Assert.AreEqual(1, _manager.OpenCount, "lower popup untouched");

            lowerPopup.CloseWith("done");
            lower.GetAwaiter().GetResult();
        }

        [Test]
        public void TryHandleBack_ReturnsFalse_WhenNothingOpen()
        {
            _manager = new PopupManager(new FakeLoader(Prefab()), new GatedPopupTransition());
            Assert.IsFalse(_manager.TryHandleBack());
        }

        [Test]
        public void TryHandleBack_DoesNotDismiss_WhenOptionDisabled()
        {
            _manager = new PopupManager(new FakeLoader(Prefab()), new GatedPopupTransition());
            var show = _manager.ShowAsync<object>(AnyRef, options: new PopupOptions { DismissOnBack = false });

            Assert.IsTrue(_manager.TryHandleBack(), "still consumed");
            Assert.AreEqual(1, _manager.OpenCount, "but not dismissed");

            Top.CloseWith("done");
            show.GetAwaiter().GetResult();
        }

        [Test]
        public void CloseAll_EmptiesTheLayer()
        {
            _manager = new PopupManager(new FakeLoader(Prefab()), new GatedPopupTransition());
            var a = _manager.ShowAsync<object>(AnyRef);
            var b = _manager.ShowAsync<object>(AnyRef);
            Assert.AreEqual(2, _manager.OpenCount);

            _manager.CloseAll();
            Assert.AreEqual(0, _manager.OpenCount);
            Assert.AreEqual(PopupResult.Cancelled, a.GetAwaiter().GetResult());
            Assert.AreEqual(PopupResult.Cancelled, b.GetAwaiter().GetResult());
        }

        [Test]
        public void Dispose_CancelsInFlight_AndEmptiesTheLayer()
        {
            _manager = new PopupManager(new FakeLoader(Prefab()), new GatedPopupTransition());
            var show = _manager.ShowAsync<object>(AnyRef);
            Assert.AreEqual(1, _manager.OpenCount);

            _manager.Dispose();
            Assert.AreEqual(0, _manager.OpenCount);

            bool cancelled = false;
            try { show.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert.IsTrue(cancelled, "in-flight show cancelled by Dispose");
        }

        [Test]
        public void Dispose_DuringInFlightLoad_CancelsLoad_AndReleasesNothing()
        {
            // The load is gated open, so the show parks inside LoadPrefabAsync (before any instance/entry exists).
            var loader = new FakeLoader(Prefab()) { LoadGate = new UniTaskCompletionSource() };
            _manager = new PopupManager(loader, new GatedPopupTransition());

            var show = _manager.ShowAsync<bool>(AnyRef);
            Assert.AreEqual(0, _manager.OpenCount, "nothing registered while still loading");

            _manager.Dispose(); // must cancel the in-flight load through the linked token

            bool cancelled = false;
            try { show.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert.IsTrue(cancelled, "Dispose cancels the in-flight load");
            Assert.AreEqual(0, loader.ReleaseCount, "nothing was loaded, so nothing is released");
        }

        [Test]
        public void CallerToken_Cancels_LeavesLayerClean_AndPropagates()
        {
            var loader = new FakeLoader(Prefab());
            _manager = new PopupManager(loader, new GatedPopupTransition());
            var cts = new CancellationTokenSource();

            var show = _manager.ShowAsync<bool>(AnyRef, ct: cts.Token);
            Assert.AreEqual(1, _manager.OpenCount);

            cts.Cancel();
            Assert.AreEqual(0, _manager.OpenCount, "finally tore the popup down");
            Assert.AreEqual(1, loader.ReleaseCount);

            bool cancelled = false;
            try { show.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert.IsTrue(cancelled);
        }

        [Test]
        public void ShowAsync_PassesArgToOnOpened()
        {
            _manager = new PopupManager(new FakeLoader(Prefab()), new GatedPopupTransition());
            var show = _manager.ShowAsync<bool>(AnyRef, arg: "hello");
            Assert.AreEqual("hello", Top.LastArg);
            Top.CloseWith(true);
            show.GetAwaiter().GetResult();
        }

        [Test]
        public void ShowAsync_ById_ThrowsWithoutRegistry()
        {
            _manager = new PopupManager(new FakeLoader(Prefab()), new GatedPopupTransition());
            Assert.Throws<InvalidOperationException>(() => _manager.ShowAsync<bool>((object)"some-id"));
        }

        [Test]
        public void ShowAsync_ById_ResolvesThroughRegistry()
        {
            var registry = new PopupRegistry().Map("confirm", AnyRef);
            var loader = new FakeLoader(Prefab());
            _manager = new PopupManager(loader, new GatedPopupTransition(), registry);

            var show = _manager.ShowAsync<bool>((object)"confirm");
            Assert.AreEqual(1, _manager.OpenCount);
            Top.CloseWith(true);
            Assert.IsTrue(show.GetAwaiter().GetResult());
        }
    }
}
