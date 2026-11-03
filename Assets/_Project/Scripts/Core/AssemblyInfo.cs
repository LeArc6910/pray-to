using System.Runtime.CompilerServices;

// The Core tests may set what the game keeps internal (a monster's health, for one), to set up a case directly; so
// may the balance sandbox (DevTools/CoreSandbox), to start a rift at a given tier.
[assembly: InternalsVisibleTo("Game.Core.Tests")]
[assembly: InternalsVisibleTo("CoreSandbox")]
