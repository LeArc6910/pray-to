using System.Runtime.CompilerServices;

// The play mode tests start sessions through GameRoot's internal entry points.
[assembly: InternalsVisibleTo("Game.Client.Tests")]
