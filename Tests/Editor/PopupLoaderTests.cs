using System;
using NUnit.Framework;
using UnityEngine;

namespace KidzDev.Unity.Popup.Tests
{
    [TestFixture]
    internal sealed class PopupLoaderTests
    {
        [Test]
        public void Composite_RoutesEachSourceToItsLoader()
        {
            var res = new RecordingLoader();
            var dir = new RecordingLoader();
            var addr = new RecordingLoader();
            var composite = new CompositePopupLoader()
                .With(PopupSource.Resources, res)
                .With(PopupSource.Direct, dir)
                .With(PopupSource.Addressables, addr);

            composite.LoadPrefabAsync(PopupRef.Resources("a"), default).GetAwaiter().GetResult();
            composite.LoadPrefabAsync(PopupRef.Addressables("b"), default).GetAwaiter().GetResult();

            Assert.AreEqual(1, res.LoadCount);
            Assert.AreEqual("a", res.LastReference.Location);
            Assert.AreEqual(1, addr.LoadCount);
            Assert.AreEqual("b", addr.LastReference.Location);
            Assert.AreEqual(0, dir.LoadCount, "Direct loader untouched");
        }

        [Test]
        public void Composite_UnregisteredSource_ThrowsClearError()
        {
            var composite = new CompositePopupLoader()
                .With(PopupSource.Resources, new RecordingLoader());

            var ex = Assert.Throws<InvalidOperationException>(
                () => composite.LoadPrefabAsync(PopupRef.Direct(null), default));
            StringAssert.Contains("Direct", ex.Message);
        }

        [Test]
        public void Composite_CreateDefault_HasResourcesAndDirect()
        {
            var composite = CompositePopupLoader.CreateDefault();
            var prefab = new GameObject("p");
            try
            {
                var loaded = composite.LoadPrefabAsync(PopupRef.Direct(prefab), default).GetAwaiter().GetResult();
                Assert.AreSame(prefab, loaded, "Direct source returns the inspector prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Direct_NullPrefab_Throws()
        {
            var loader = new DirectPopupLoader();
            Assert.Throws<InvalidOperationException>(
                () => loader.LoadPrefabAsync(PopupRef.Direct(null), default).GetAwaiter().GetResult());
        }

        [Test]
        public void Resources_WrongSource_Throws()
        {
            var loader = new ResourcesPopupLoader();
            Assert.Throws<InvalidOperationException>(
                () => loader.LoadPrefabAsync(PopupRef.Direct(null), default).GetAwaiter().GetResult());
        }

        [Test]
        public void Registry_Get_ThrowsForUnknownId()
        {
            var registry = new PopupRegistry().Map("known", PopupRef.Resources("x"));
            Assert.IsTrue(registry.TryGet("known", out _));
            Assert.IsFalse(registry.TryGet("missing", out _));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => registry.Get("missing"));
        }

        [Test]
        public void PopupRef_Factories_SetSourceAndValidity()
        {
            Assert.AreEqual(PopupSource.Resources, PopupRef.Resources("p").Source);
            Assert.AreEqual(PopupSource.Addressables, PopupRef.Addressables("k").Source);
            Assert.AreEqual(PopupSource.Direct, PopupRef.Direct(null).Source);

            Assert.IsTrue(PopupRef.Resources("p").IsValid);
            Assert.IsFalse(PopupRef.Resources("").IsValid);
            Assert.IsFalse(PopupRef.Direct(null).IsValid);
        }
    }
}
