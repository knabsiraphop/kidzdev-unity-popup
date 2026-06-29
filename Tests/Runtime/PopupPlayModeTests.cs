using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace KidzDev.Unity.Popup.Tests
{
    /// <summary>A concrete <see cref="Popup"/> for PlayMode transition/backdrop tests.</summary>
    internal sealed class PmPopup : Popup { }

    [TestFixture]
    internal sealed class PopupPlayModeTests
    {
        private static PmPopup MakePopup(out GameObject go)
        {
            go = new GameObject("PmPopup", typeof(RectTransform), typeof(CanvasGroup));
            return go.AddComponent<PmPopup>();
        }

        private static GameObject MakePrefab()
        {
            var go = new GameObject("PmPopupPrefab", typeof(RectTransform), typeof(CanvasGroup));
            go.AddComponent<PmPopup>();
            go.SetActive(false);
            return go;
        }

        [UnityTest]
        public IEnumerator Fade_Enter_EndsAtFullAlpha_Exit_EndsAtZero() => UniTask.ToCoroutine(async () =>
        {
            var popup = MakePopup(out var go);
            var cg = go.GetComponent<CanvasGroup>();
            var transition = new FadePopupTransition(0.05f);
            try
            {
                await transition.PlayEnterAsync(popup, default);
                Assert.AreEqual(1f, cg.alpha, 0.001f, "enter ends fully visible");

                await transition.PlayExitAsync(popup, default);
                Assert.AreEqual(0f, cg.alpha, 0.001f, "exit ends fully transparent");
            }
            finally { UnityEngine.Object.Destroy(go); }
        });

        [UnityTest]
        public IEnumerator Scale_Enter_EndsAtUnitScale() => UniTask.ToCoroutine(async () =>
        {
            var popup = MakePopup(out var go);
            var transition = new ScalePopupTransition(0.05f, 0.8f);
            try
            {
                await transition.PlayEnterAsync(popup, default);
                Assert.AreEqual(Vector3.one, go.transform.localScale, "enter ends at full scale");
                Assert.AreEqual(1f, go.GetComponent<CanvasGroup>().alpha, 0.001f);
            }
            finally { UnityEngine.Object.Destroy(go); }
        });

        [UnityTest]
        public IEnumerator Backdrop_Tap_DismissesPopup_WhenOptionEnabled() => UniTask.ToCoroutine(async () =>
        {
            var prefab = MakePrefab();
            var manager = new PopupManager(transition: new InstantPopupTransition());
            try
            {
                var options = new PopupOptions { DismissOnBackdropClick = true };
                var show = manager.ShowAsync<PopupResult>(PopupRef.Direct(prefab), options: options);
                await UniTask.NextFrame();
                Assert.AreEqual(1, manager.OpenCount);

                var backdrop = UnityEngine.Object.FindAnyObjectByType<PopupBackdrop>();
                Assert.IsNotNull(backdrop, "a backdrop was raised");
                backdrop.OnPointerClick(new PointerEventData(EventSystem.current));

                var result = await show;
                Assert.AreEqual(PopupResult.Cancelled, result, "backdrop tap dismisses with Cancelled");
                Assert.AreEqual(0, manager.OpenCount);
            }
            finally
            {
                manager.Dispose();
                UnityEngine.Object.Destroy(prefab);
            }
        });

        [UnityTest]
        public IEnumerator Dispose_CancelsInFlightLongFade() => UniTask.ToCoroutine(async () =>
        {
            var prefab = MakePrefab();
            var manager = new PopupManager(transition: new FadePopupTransition(5f)); // long enter
            try
            {
                var show = manager.ShowAsync<PopupResult>(PopupRef.Direct(prefab));
                await UniTask.NextFrame();
                Assert.AreEqual(1, manager.OpenCount, "long fade in flight");

                manager.Dispose();

                bool cancelled = false;
                try { await show; }
                catch (OperationCanceledException) { cancelled = true; }
                Assert.IsTrue(cancelled, "Dispose cancels the in-flight fade");
            }
            finally { UnityEngine.Object.Destroy(prefab); }
        });

        [UnityTest]
        public IEnumerator CallerToken_CancelsInFlightLongFade() => UniTask.ToCoroutine(async () =>
        {
            var prefab = MakePrefab();
            var manager = new PopupManager(transition: new FadePopupTransition(5f));
            var cts = new CancellationTokenSource();
            try
            {
                var show = manager.ShowAsync<PopupResult>(PopupRef.Direct(prefab), ct: cts.Token);
                await UniTask.NextFrame();
                Assert.AreEqual(1, manager.OpenCount);

                cts.Cancel();

                bool cancelled = false;
                try { await show; }
                catch (OperationCanceledException) { cancelled = true; }
                Assert.IsTrue(cancelled, "caller token cancels the in-flight fade");
                Assert.AreEqual(0, manager.OpenCount);
            }
            finally
            {
                manager.Dispose();
                UnityEngine.Object.Destroy(prefab);
            }
        });
    }
}
