using System.Runtime.CompilerServices;

// The EditMode tests exercise internal types (UpgradeManager's selection logic) without
// making them public just to be reachable from a test.
[assembly: InternalsVisibleTo("Sentaur.Tests.EditMode")]
[assembly: InternalsVisibleTo("Sentaur.Tests.PlayMode")]
