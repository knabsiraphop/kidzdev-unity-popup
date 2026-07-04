using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace KidzDev.Unity.Popup.Tests
{
    [TestFixture]
    internal sealed class PopupStackControllerTests
    {
        private readonly List<GameObject> _prefabs = new List<GameObject>();
        private PopupStackController _stack;

        [SetUp]
        public void SetUp() => FakePopup.ResetLive();

        [TearDown]
        public void TearDown()
        {
            _stack?.Dispose();
            _stack = null;
            foreach (var p in _prefabs)
                if (p != null) UnityEngine.Object.DestroyImmediate(p);
            _prefabs.Clear();
            for (var layer = GameObject.Find("[PopupStackLayer]"); layer != null; layer = GameObject.Find("[PopupStackLayer]"))
                UnityEngine.Object.DestroyImmediate(layer);
            FakePopup.ResetLive();
        }

        private GameObject Prefab()
        {
            var p = FakePopup.CreatePrefab();
            _prefabs.Add(p);
            return p;
        }

        private static readonly PopupRef AnyRef = PopupRef.Resources("x");

        [Test]
        public void Enqueue_BelowCap_ShowsImmediately()
        {
            var loader = new FakeLoader(Prefab());
            var layout = new RecordingStackLayout(maxVisible: 3);
            _stack = new PopupStackController(layout, loader);

            var show = _stack.Enqueue<bool>(AnyRef);

            Assert.AreEqual(1, _stack.VisibleCount, "shown immediately, under cap");
            Assert.AreEqual(0, _stack.QueuedCount);
            Assert.AreEqual(1, loader.LoadCount);

            FakePopup.Live[0].CloseWith(true);
            Assert.IsTrue(show.GetAwaiter().GetResult());
        }

        [Test]
        public void Enqueue_AtCap_Queues_DoesNotLoad()
        {
            var loader = new FakeLoader(Prefab());
            var layout = new RecordingStackLayout(maxVisible: 2);
            _stack = new PopupStackController(layout, loader);

            var a = _stack.Enqueue<object>(AnyRef);
            var b = _stack.Enqueue<object>(AnyRef);
            var c = _stack.Enqueue<object>(AnyRef);

            Assert.AreEqual(2, _stack.VisibleCount, "capped at MaxVisible");
            Assert.AreEqual(1, _stack.QueuedCount, "the 3rd waits");
            Assert.AreEqual(2, loader.LoadCount, "queued card not loaded yet");

            // Drain everything. Closing a card synchronously cascades into promoting the next queued one (its
            // own instance is added to FakePopup.Live mid-drain), so re-read Live fresh each iteration rather
            // than iterating a snapshot — this is exactly the cascade Collect_PromotesNextQueued asserts on
            // directly; here we just need the whole stack to settle.
            while (FakePopup.Live.Count > 0)
                FakePopup.Live[0].CloseWith("done");

            a.GetAwaiter().GetResult();
            b.GetAwaiter().GetResult();
            c.GetAwaiter().GetResult();
        }

        [Test]
        public void Collect_PromotesNextQueued()
        {
            var loader = new FakeLoader(Prefab());
            var layout = new RecordingStackLayout(maxVisible: 1);
            _stack = new PopupStackController(layout, loader);

            var a = _stack.Enqueue<object>(AnyRef);
            var b = _stack.Enqueue<object>(AnyRef);
            Assert.AreEqual(1, _stack.VisibleCount);
            Assert.AreEqual(1, _stack.QueuedCount);
            Assert.AreEqual(1, loader.LoadCount, "2nd not loaded while 1st occupies the only slot");

            FakePopup.Live[0].CloseWith("first");
            Assert.AreEqual("first", a.GetAwaiter().GetResult());

            Assert.AreEqual(1, _stack.VisibleCount, "2nd promoted into the freed slot");
            Assert.AreEqual(0, _stack.QueuedCount);
            Assert.AreEqual(2, loader.LoadCount);

            FakePopup.Live[0].CloseWith("second");
            Assert.AreEqual("second", b.GetAwaiter().GetResult());
            Assert.AreEqual(0, _stack.VisibleCount);
        }

        [Test]
        public void Reflow_Runs_OnEveryVisibleSetChange()
        {
            var loader = new FakeLoader(Prefab());
            var layout = new RecordingStackLayout(maxVisible: 2);
            _stack = new PopupStackController(layout, loader);

            var a = _stack.Enqueue<object>(AnyRef);
            Assert.AreEqual(1, layout.ArrangedCounts[layout.ArrangedCounts.Count - 1], "reflow after 1st promotion");

            var b = _stack.Enqueue<object>(AnyRef);
            Assert.AreEqual(2, layout.ArrangedCounts[layout.ArrangedCounts.Count - 1], "reflow after 2nd promotion");

            FakePopup.Live[0].CloseWith("done");
            a.GetAwaiter().GetResult();
            Assert.AreEqual(1, layout.ArrangedCounts[layout.ArrangedCounts.Count - 1], "reflow after collecting one");

            FakePopup.Live[0].CloseWith("done");
            b.GetAwaiter().GetResult();
        }

        [Test]
        public void Layout_IsInteractable_GatesCanvasGroup()
        {
            var loader = new FakeLoader(Prefab());
            var layout = new RecordingStackLayout(maxVisible: 2) { InteractablePredicate = (i, count) => i == 0 };
            _stack = new PopupStackController(layout, loader);

            var a = _stack.Enqueue<object>(AnyRef);
            var b = _stack.Enqueue<object>(AnyRef);

            var front = FakePopup.Live[0].GetComponent<CanvasGroup>();
            var back = FakePopup.Live[1].GetComponent<CanvasGroup>();
            Assert.IsTrue(front.interactable && front.blocksRaycasts, "front card interactive");
            Assert.IsFalse(back.interactable || back.blocksRaycasts, "back card gated off");

            FakePopup.Live[0].CloseWith("done");
            a.GetAwaiter().GetResult();
            FakePopup.Live[0].CloseWith("done");
            b.GetAwaiter().GetResult();
        }

        [Test]
        public void ClearAll_DismissesActive_CancelsQueued()
        {
            var loader = new FakeLoader(Prefab());
            var layout = new RecordingStackLayout(maxVisible: 1);
            _stack = new PopupStackController(layout, loader);

            var active = _stack.Enqueue<object>(AnyRef);
            var queued = _stack.Enqueue<object>(AnyRef);
            Assert.AreEqual(1, loader.LoadCount, "queued card not loaded yet");

            _stack.ClearAll();

            Assert.AreEqual(PopupResult.Cancelled, active.GetAwaiter().GetResult(), "active dismissed via TryDismiss");

            bool cancelled = false;
            try { queued.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert.IsTrue(cancelled, "queued card cancelled without ever loading");
            Assert.AreEqual(1, loader.LoadCount, "still never loaded");
        }

        [Test]
        public void Dispose_CancelsInFlight_TearsDownLayer()
        {
            var loader = new FakeLoader(Prefab());
            var layout = new RecordingStackLayout(maxVisible: 1);
            _stack = new PopupStackController(layout, loader);

            var active = _stack.Enqueue<object>(AnyRef);
            var queued = _stack.Enqueue<object>(AnyRef);
            Assert.AreEqual(1, _stack.VisibleCount);

            _stack.Dispose();
            Assert.AreEqual(0, _stack.VisibleCount);
            Assert.IsNull(GameObject.Find("[PopupStackLayer]"));

            bool activeCancelled = false;
            try { active.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { activeCancelled = true; }
            Assert.IsTrue(activeCancelled);

            bool queuedCancelled = false;
            try { queued.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { queuedCancelled = true; }
            Assert.IsTrue(queuedCancelled);
        }

        [Test]
        public void BackdropAnchor_PlacesBackdropAsSiblingBeforeAnchor_NotInControllersOwnLayer()
        {
            // Simulates GroupStackLayout relocating cards into a caller-owned canvas: some other UI the
            // backdrop must dim/block sits before the anchor, and the anchor itself represents where the
            // layout's cards will render (e.g. a LayoutGroup container).
            var host = new GameObject("HostCanvas", typeof(RectTransform)).transform;
            var otherUi = new GameObject("OtherButton", typeof(RectTransform)).transform;
            otherUi.SetParent(host, false);
            var anchor = new GameObject("GroupContainer", typeof(RectTransform)).transform;
            anchor.SetParent(host, false);
            var anchorIndexBefore = anchor.GetSiblingIndex();

            try
            {
                var loader = new FakeLoader(Prefab());
                var layout = new RecordingStackLayout(maxVisible: 1) { BackdropAnchor = anchor };
                _stack = new PopupStackController(layout, loader, backdropColor: Color.black);

                var show = _stack.Enqueue<object>(AnyRef);

                // The controller's own "[PopupStackLayer]" still exists (cards are briefly instantiated there
                // before a layout reparents them), but the backdrop itself must NOT be one of its children —
                // that's the actual bug this locks in: a backdrop left in that separate, higher-sorted overlay
                // canvas silently eats clicks meant for cards relocated into the anchor's canvas.
                var backdrop = host.Find("PopupBackdrop");
                Assert.IsNotNull(backdrop, "backdrop raised under the anchor's parent, not the controller's own layer");
                Assert.AreEqual(anchorIndexBefore, backdrop.GetSiblingIndex(), "backdrop sits immediately before the anchor");
                Assert.AreEqual(anchorIndexBefore + 1, anchor.GetSiblingIndex(), "anchor pushed one slot later by the inserted backdrop");

                FakePopup.Live[0].CloseWith("done");
                show.GetAwaiter().GetResult();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host.gameObject);
            }
        }

        [Test]
        public void AutoDismiss_ClosesCard_WhenTimerFires()
        {
            var loader = new FakeLoader(Prefab());
            var layout = new RecordingStackLayout(maxVisible: 3);
            var timer = new GatedTimer();
            _stack = new PopupStackController(layout, loader, autoDismissTimer: timer.Delay);

            var show = _stack.Enqueue<PopupResult>(AnyRef, autoDismissAfter: 2f);
            Assert.AreEqual(1, timer.Calls);
            Assert.AreEqual(2f, timer.LastSeconds);

            timer.Fire();
            Assert.AreEqual(PopupResult.Cancelled, show.GetAwaiter().GetResult());
            Assert.AreEqual(0, _stack.VisibleCount);
        }

        [Test]
        public void AutoDismiss_CollectBeforeTimer_KeepsCollectResult()
        {
            var timer = new GatedTimer();
            _stack = new PopupStackController(new RecordingStackLayout(maxVisible: 3), new FakeLoader(Prefab()), autoDismissTimer: timer.Delay);

            var show = _stack.Enqueue<bool>(AnyRef, autoDismissAfter: 2f);
            FakePopup.Live[0].CloseWith(true);
            Assert.IsTrue(show.GetAwaiter().GetResult());

            // The timer firing after close must be a no-op (the card's instance is already torn down).
            Assert.DoesNotThrow(() => timer.Fire());
        }

        [Test]
        public void AutoDismiss_VetoingCard_StaysOpen()
        {
            var timer = new GatedTimer();
            _stack = new PopupStackController(new RecordingStackLayout(maxVisible: 3), new FakeLoader(Prefab()), autoDismissTimer: timer.Delay);

            var show = _stack.Enqueue<object>(AnyRef, autoDismissAfter: 2f);
            FakePopup.Live[0].VetoDismiss = true;

            timer.Fire();
            Assert.AreEqual(1, _stack.VisibleCount, "vetoing card stays open after the timer fires");

            FakePopup.Live[0].VetoDismiss = false;
            FakePopup.Live[0].CloseWith("done");
            Assert.AreEqual("done", show.GetAwaiter().GetResult());
        }

        [Test]
        public void AutoDismiss_Zero_NeverArmsTimer()
        {
            var timer = new GatedTimer();
            _stack = new PopupStackController(new RecordingStackLayout(maxVisible: 3), new FakeLoader(Prefab()), autoDismissTimer: timer.Delay);

            var show = _stack.Enqueue<object>(AnyRef); // autoDismissAfter defaults to 0
            Assert.AreEqual(0, timer.Calls);

            FakePopup.Live[0].CloseWith("done");
            show.GetAwaiter().GetResult();
        }

        [Test]
        public void AutoDismiss_ArmsOnlyAfterEnterTransition()
        {
            var timer = new GatedTimer();
            var transition = new GatedPopupTransition { Open = false };
            _stack = new PopupStackController(new RecordingStackLayout(maxVisible: 3), new FakeLoader(Prefab()), transition, autoDismissTimer: timer.Delay);

            var show = _stack.Enqueue<object>(AnyRef, autoDismissAfter: 2f);
            Assert.AreEqual(0, timer.Calls, "not armed until the enter transition completes");

            transition.Release(); // enter transition completes
            Assert.AreEqual(1, timer.Calls, "armed right after enter");

            FakePopup.Live[0].CloseWith("done");
            show.GetAwaiter().GetResult();
        }

        [Test]
        public void AutoDismiss_QueuedCard_DoesNotArmTimer_UntilPromoted()
        {
            var timer = new GatedTimer();
            _stack = new PopupStackController(new RecordingStackLayout(maxVisible: 1), new FakeLoader(Prefab()), autoDismissTimer: timer.Delay);

            var active = _stack.Enqueue<object>(AnyRef, autoDismissAfter: 2f);
            var queued = _stack.Enqueue<object>(AnyRef, autoDismissAfter: 3f);
            Assert.AreEqual(1, timer.Calls, "only the shown card's timer is armed");
            Assert.AreEqual(2f, timer.LastSeconds);

            FakePopup.Live[0].CloseWith("done");
            Assert.AreEqual("done", active.GetAwaiter().GetResult());

            Assert.AreEqual(2, timer.Calls, "promoted card arms its own timer once actually shown");
            Assert.AreEqual(3f, timer.LastSeconds);

            timer.Fire();
            Assert.AreEqual(PopupResult.Cancelled, queued.GetAwaiter().GetResult());
        }

        [Test]
        public void Dispose_CancelsAutoDismissTimer()
        {
            var timer = new GatedTimer();
            _stack = new PopupStackController(new RecordingStackLayout(maxVisible: 3), new FakeLoader(Prefab()), autoDismissTimer: timer.Delay);

            var show = _stack.Enqueue<object>(AnyRef, autoDismissAfter: 2f);
            Assert.AreEqual(1, timer.Calls);

            _stack.Dispose();
            Assert.AreEqual(0, _stack.VisibleCount);

            bool cancelled = false;
            try { show.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert.IsTrue(cancelled, "in-flight card cancelled by Dispose");
        }
    }
}
