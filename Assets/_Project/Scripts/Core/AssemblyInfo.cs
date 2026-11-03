using System.Runtime.CompilerServices;

// The Core tests may set what the game keeps internal (a monster's health, for one), to set up a case directly; so
// may the balance sandbox (DevTools/CoreSandbox), to start a rift at a given tier, and the play mode tests, to set the
// host's world up for a scene (the clock, a machine's contents).
[assembly: InternalsVisibleTo("Game.Core.Tests")]
[assembly: InternalsVisibleTo("CoreSandbox")]
[assembly: InternalsVisibleTo("Game.Client.Tests")]
