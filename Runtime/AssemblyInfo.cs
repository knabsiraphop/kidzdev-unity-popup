using System.Runtime.CompilerServices;

// Exposes internal helpers to the test assemblies so manager bookkeeping and the
// loader/transition seams can be exercised directly with fakes.
[assembly: InternalsVisibleTo("KidzDev.Unity.Popup.Tests.Editor")]
[assembly: InternalsVisibleTo("KidzDev.Unity.Popup.Tests.Runtime")]
