using System.Runtime.CompilerServices;

// The Core tests may set what the game keeps internal (a monster's health, for one), to set up a case directly.
[assembly: InternalsVisibleTo("Game.Core.Tests")]
