using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // The people of the story and the towns' people near the player, nudged apart before they are drawn so they do
    // not walk through one another (Core Crowd, the owner's ask of 2026-11-29). Where the clock puts each is worked
    // out once here for both DrawStoryPeople and DrawTownsfolk; each then draws its people where Spread says.
    sealed partial class WorldView
    {
        readonly Dictionary<object, Vec2> _spread = new Dictionary<object, Vec2>();
        readonly List<object> _crowdKeys = new List<object>();
        readonly List<Vec2> _crowdAt = new List<Vec2>();
        readonly List<Vec2> _crowdHeading = new List<Vec2>();

        void SpreadPeople()
        {
            _spread.Clear();
            _crowdKeys.Clear();
            _crowdAt.Clear();
            _crowdHeading.Clear();
            var self = new Vec2(_self.Position.x, _self.Position.y);
            long clock = _client.Clock;
            int day = _client.DayMinutes;
            float range = PeopleRange + Crowd.Gap;
            void Add(object key, Vec2 at, Vec2 before)
            {
                if (Vec2.Distance(self, at) > range) return;
                _crowdKeys.Add(key);
                _crowdAt.Add(at);
                _crowdHeading.Add(at - before);
            }
            var kinds = _client.Story?.Townsfolk;
            foreach (var site in _client.Places)
            {
                if (Vec2.Distance(self, site.Center) > PeopleRange + site.Radius) continue;
                foreach (var (character, home) in site.People)
                    Add(character.Id, site.PersonAt(character, home, clock, day), site.PersonAt(character, home, clock - 6, day));
                if (kinds == null || kinds.Count == 0 || site.Radius == site.Definition.Radius) continue;
                if (!_townsfolk.TryGetValue(site, out var people)) _townsfolk[site] = people = Townsfolk.Of(site, _client.Map, kinds, _client.Seed);
                foreach (var person in people) Add(person, person.At(clock, day), person.At(clock - 6, day));
            }
            if (_crowdKeys.Count < 2) return;
            var spread = Crowd.Spread(_crowdAt, _crowdHeading, _client.Map);
            for (int i = 0; i < _crowdKeys.Count; i++) _spread[_crowdKeys[i]] = spread[i];
        }

        // Where someone is drawn: nudged apart from the rest, or where the clock puts them.
        Vec2 Spread(object key, Vec2 at) => _spread.TryGetValue(key, out var p) ? p : at;
    }
}
