using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // What moves in a biome besides its monsters (Docs/BiomeRework.md 4-1 작은 생물·입자, 4-3 웹 성능 6, 묶음 B1a): the
    // motes in the air (dust in a lamp's light and drips from the ceiling in the dirt cave, lime spores rising in the
    // mushroom forest) and the small creatures (a cricket, a mouse that runs from whoever comes near, moths round a
    // lamp; fireflies, more at night, a snail that leaves a glowing trail, spore moths). Only looked at: nothing here
    // is in the game's state, saved or sent, and every screen has its own. For a browser: pooled renderers, at most
    // MoteCap motes (80, on a phone 40) and CritterCap creatures (8, on a phone 5) round the player; the creatures think
    // ten times a second; no tile is looked for (lamps come from the standing layer's list of what is in view). With
    // 움직임: 줄이기 the motes are off.
    // 묶음 B2a adds the ice cave's snowflakes, glints of frost, white hare and ice moths, and the sand cave's blown dust,
    // grains sifting down the rock, lizard and scarab.
    partial class WorldView
    {
        struct Mote
        {
            public MoteKind Kind;
            public Vector2 At, Speed;
            public float Age, Life, Phase;
        }

        sealed class Critter
        {
            public CritterKind Kind;
            public Vector2 At, Heading, Home;
            public float Think, Busy, Trail, Phase;
            public bool Fleeing, Left;
            public SpriteRenderer Renderer;
        }

        readonly List<Mote> _motes = new List<Mote>();
        readonly List<SpriteRenderer> _motePool = new List<SpriteRenderer>();
        readonly List<Critter> _critters = new List<Critter>();
        readonly List<SpriteRenderer> _critterPool = new List<SpriteRenderer>();
        readonly List<Vector2> _lamps = new List<Vector2>();
        bool[] _burns;
        float _moteClock, _dripClock, _critterClock, _critterThink;

        // On a phone half the motes and fewer creatures (4-3 웹 성능 6).
        static bool SmallScreen => Controls.UsingTouch || Application.isMobilePlatform;
        public static int MoteCap => SmallScreen ? 40 : 80;
        public static int CritterCap => SmallScreen ? 5 : 8;
        // Creatures live within this far of the player and come into being at least this far off.
        const float CritterReach = 20, CritterNear = 7;
        const float ThinkSeconds = 0.1f;
        // Dust is under the dark (seen only in light); what glows is over it.
        const int DustOrder = 9100, GlowOrder = DarkOrder + 30, TrailOrder = 61, FlyOrder = 9050;

        // For the playtests: what is alive now.
        public int MotesDrawn { get; private set; }
        public int CrittersAlive => _critters.Count;
        public int LampsInView => _lamps.Count;
        // Critters of another biome than the spot they came into being on (the playtest's check: near a border both
        // biomes' kinds may be about, but each stands where its own biome is).
        public int CrittersAstray
        {
            get
            {
                int astray = 0;
                foreach (var critter in _critters)
                    if (CritterArt.HomeOf(critter.Kind) != BiomeOf(critter.Home)) astray++;
                return astray;
            }
        }

        public string CritterKinds
        {
            get
            {
                var names = new SortedSet<string>();
                foreach (var critter in _critters) names.Add(critter.Kind.ToString());
                return string.Join(",", names);
            }
        }

        void DrawAmbience(float deltaTime)
        {
            var map = _client.Map;
            if (map == null || _self == null) return;
            var self = new Vector2(_self.Position.x, _self.Position.y);
            // The lamps and torches in view, from the standing layer's list.
            if (_burns == null)
            {
                _burns = new bool[Enum.GetValues(typeof(TileType)).Length];
                foreach (var (type, radius) in TileLight.Sources(_client.Tiles))
                    if ((int)type < _burns.Length && radius >= 4) _burns[(int)type] = true;
            }
            _lamps.Clear();
            foreach (var stand in _standsInView)
                if (_burns[(int)stand.Type] && _lamps.Count < 16) _lamps.Add(new Vector2(stand.X + 0.5f, stand.Y + 0.9f));
            UpdateMotes(map, self, deltaTime);
            UpdateCritters(map, self, deltaTime);
        }

        Biome BiomeOf(Vector2 at) => _client.Biomes[Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)];

        static bool Walkable(TileMap map, Vector2 at)
        {
            int x = Mathf.FloorToInt(at.x), y = Mathf.FloorToInt(at.y);
            return map.InBounds(x, y) && !map.IsSolid(x, y) && map[x, y] != TileType.Lava;
        }

        // ---- Motes ----

        void UpdateMotes(TileMap map, Vector2 self, float deltaTime)
        {
            int cap = Settings.ReduceMotion ? 0 : MoteCap;
            float high = _camera.orthographicSize, wide = high * _camera.aspect;
            var middle = (Vector2)_camera.transform.position;
            var biome = BiomeOf(self);
            // New ones, a few a second, where the player's biome has them.
            _moteClock += deltaTime;
            while (_moteClock > 0.12f)
            {
                _moteClock -= 0.12f;
                if (_motes.Count >= cap) break;
                if (biome == Biome.Mushroom)
                {
                    // A spore rising slowly from somewhere on the floor in view.
                    var at = middle + new Vector2((UnityEngine.Random.value - 0.5f) * wide * 2, (UnityEngine.Random.value - 0.5f) * high * 2);
                    if (Walkable(map, at) && BiomeOf(at) == Biome.Mushroom)
                        _motes.Add(new Mote { Kind = MoteKind.Spore, At = at, Speed = new Vector2(0, 0.22f + UnityEngine.Random.value * 0.2f), Life = 4 + UnityEngine.Random.value * 3, Phase = UnityEngine.Random.value * 6.28f });
                }
                else if (biome == Biome.Ice)
                {
                    // A snowflake drifting down across the view (묶음 B2a), and now and then a glint of frost on the floor.
                    var at = middle + new Vector2((UnityEngine.Random.value - 0.5f) * wide * 2, (UnityEngine.Random.value - 0.3f) * high * 2);
                    if (BiomeOf(at) == Biome.Ice && !LightMap.BlocksLight(map[Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)]))
                        _motes.Add(new Mote { Kind = MoteKind.Flake, At = at, Speed = new Vector2(-0.12f + UnityEngine.Random.value * 0.1f, -0.45f - UnityEngine.Random.value * 0.3f), Life = 4 + UnityEngine.Random.value * 3, Phase = UnityEngine.Random.value * 6.28f });
                    if (UnityEngine.Random.value < 0.3f && _motes.Count < cap)
                    {
                        var on = middle + new Vector2((UnityEngine.Random.value - 0.5f) * wide * 2, (UnityEngine.Random.value - 0.5f) * high * 2);
                        if (Walkable(map, on) && BiomeOf(on) == Biome.Ice) _motes.Add(new Mote { Kind = MoteKind.Glint, At = on, Life = 0.5f + UnityEngine.Random.value * 0.4f });
                    }
                }
                else if (biome == Biome.Sand)
                {
                    // Dust blown low over the floor, and grains sifting down a rock face (only at the rock: sand running
                    // onto open floor is the sign of something buried, which the rules own).
                    var at = middle + new Vector2((UnityEngine.Random.value - 0.5f) * wide * 2, (UnityEngine.Random.value - 0.5f) * high * 2);
                    if (Walkable(map, at) && BiomeOf(at) == Biome.Sand)
                    {
                        int tx = Mathf.FloorToInt(at.x), ty = Mathf.FloorToInt(at.y);
                        if (LightMap.BlocksLight(map[tx, ty + 1]))
                        {
                            // Three grains down the face over this tile, one after another.
                            var from = new Vector2(tx + 0.2f + UnityEngine.Random.value * 0.6f, ty + 1.3f);
                            for (int i = 0; i < 3 && _motes.Count < cap; i++)
                                _motes.Add(new Mote { Kind = MoteKind.Grain, At = from + new Vector2(0, i * 0.25f), Speed = new Vector2(0, -2.2f), Life = 0.35f + i * 0.1f });
                        }
                        else if (_motes.Count < cap * 3 / 4)
                            _motes.Add(new Mote { Kind = MoteKind.SandDust, At = at, Speed = new Vector2(0.5f + UnityEngine.Random.value * 0.5f, 0.02f), Life = 2.5f + UnityEngine.Random.value * 2, Phase = UnityEngine.Random.value * 6.28f });
                    }
                }
                else if (biome == Biome.Cave && _motes.Count < cap * 3 / 4)
                {
                    // Dust in a lamp's light, or failing a lamp in the light the player carries.
                    var from = _lamps.Count > 0 && UnityEngine.Random.value < 0.75f ? _lamps[UnityEngine.Random.Range(0, _lamps.Count)] : self;
                    var at = from + UnityEngine.Random.insideUnitCircle * 2.6f;
                    if (Walkable(map, at))
                        _motes.Add(new Mote { Kind = MoteKind.Dust, At = at, Speed = UnityEngine.Random.insideUnitCircle * 0.12f + new Vector2(0, 0.04f), Life = 3 + UnityEngine.Random.value * 3, Phase = UnityEngine.Random.value * 6.28f });
                }
            }
            // A drip from the ceiling now and then in the dirt cave: it falls a tile and a half and splashes.
            _dripClock -= deltaTime;
            if (_dripClock <= 0 && biome == Biome.Cave && _motes.Count < cap)
            {
                _dripClock = 0.8f + UnityEngine.Random.value * 2.2f;
                var land = middle + new Vector2((UnityEngine.Random.value - 0.5f) * wide * 1.6f, (UnityEngine.Random.value - 0.5f) * high * 1.6f);
                if (Walkable(map, land) && BiomeOf(land) == Biome.Cave)
                    _motes.Add(new Mote { Kind = MoteKind.Drip, At = land + new Vector2(0, 1.5f), Speed = new Vector2(0, -5f), Life = 0.3f });
            }
            int used = 0;
            for (int i = _motes.Count - 1; i >= 0; i--)
            {
                var mote = _motes[i];
                mote.Age += deltaTime;
                if (mote.Age >= mote.Life || cap == 0)
                {
                    // A drip ends in its splash.
                    if (mote.Kind == MoteKind.Drip && mote.Age >= mote.Life && cap > 0) _motes[i] = new Mote { Kind = MoteKind.Splash, At = mote.At, Life = 0.22f };
                    else _motes.RemoveAt(i);
                    continue;
                }
                mote.At += mote.Speed * deltaTime;
                if (mote.Kind == MoteKind.Spore || mote.Kind == MoteKind.Dust || mote.Kind == MoteKind.Flake) mote.At.x += Mathf.Sin(Time.time * 1.3f + mote.Phase) * (mote.Kind == MoteKind.Flake ? 0.3f : 0.12f) * deltaTime;
                _motes[i] = mote;
            }
            foreach (var mote in _motes)
            {
                if (used == _motePool.Count) _motePool.Add(Renderer(_root.transform, "Mote", null, Color.white));
                var renderer = _motePool[used++];
                if (!renderer.enabled) renderer.enabled = true;
                long key = 3L << 40 | (long)mote.Kind;
                var kind = mote.Kind;
                if (!PropAtlas.TryGet(key, out var sprite)) sprite = PropAtlas.Get(key, () => CritterArt.Mote(kind));
                renderer.sprite = sprite;
                // Fading in and out over the first and last fifth of its life.
                float t = mote.Age / mote.Life;
                float alpha = mote.Kind == MoteKind.Drip || mote.Kind == MoteKind.Grain ? 0.85f : mote.Kind == MoteKind.Trail ? 0.55f * (1 - t)
                    : Mathf.Clamp01(Mathf.Min(t, 1 - t) * 5) * (mote.Kind == MoteKind.Dust || mote.Kind == MoteKind.SandDust ? 0.6f : 0.9f);
                renderer.color = new Color(1, 1, 1, alpha);
                renderer.transform.localPosition = new Vector3(mote.At.x, mote.At.y, 0);
                // A spore and a glint of frost shine over the dark; a snowflake, like dust, is seen where there is light.
                renderer.sortingOrder = mote.Kind == MoteKind.Spore || mote.Kind == MoteKind.Glint ? GlowOrder : mote.Kind == MoteKind.Trail ? TrailOrder : DustOrder;
            }
            for (int i = used; i < _motePool.Count; i++)
                if (_motePool[i].enabled) _motePool[i].enabled = false;
            MotesDrawn = used;
        }

        // ---- Critters ----

        void UpdateCritters(TileMap map, Vector2 self, float deltaTime)
        {
            bool night = Calendar.IsNight(_client.Clock);
            // One comes into being every half second while there are too few: ground ones at the edge of sight, moths at
            // a lamp.
            _critterClock -= deltaTime;
            if (_critterClock <= 0 && _critters.Count < CritterCap)
            {
                _critterClock = 0.5f;
                int moths = 0;
                foreach (var critter in _critters)
                    if (critter.Kind == CritterKind.LampMoth) moths++;
                // A moth only at a dirt cave light: a torch on the forest's edge has none (it was the first lamp's biome).
                var lamp = _lamps.Count > 0 ? _lamps[UnityEngine.Random.Range(0, _lamps.Count)] : Vector2.zero;
                if (_lamps.Count > 0 && moths < Mathf.Min(3, _lamps.Count * 2) && BiomeOf(lamp) == Biome.Cave)
                    AddCritter(CritterKind.LampMoth, lamp + UnityEngine.Random.insideUnitCircle * 0.8f, lamp);
                else
                {
                    float angle = UnityEngine.Random.value * Mathf.PI * 2;
                    var at = self + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (CritterNear + UnityEngine.Random.value * 6);
                    var biome = BiomeOf(at);
                    // Half as many by day in the forest, whose fireflies come out at night.
                    int wanted = biome == Biome.Mushroom && !night ? CritterCap * 2 / 3 : CritterCap;
                    if (Walkable(map, at) && CritterArt.Lives(biome) && _critters.Count < wanted && !InPlace(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)))
                        AddCritter(CritterArt.Pick(biome, UnityEngine.Random.Range(0, 100), night), at, at);
                }
            }
            _critterThink += deltaTime;
            bool think = _critterThink >= ThinkSeconds;
            if (think) _critterThink = 0;
            for (int i = _critters.Count - 1; i >= 0; i--)
            {
                var critter = _critters[i];
                if (think) ThinkCritter(map, critter, self);
                MoveCritter(map, critter, deltaTime);
                bool gone = (critter.At - self).sqrMagnitude > CritterReach * CritterReach
                    || critter.Kind == CritterKind.LampMoth && !LampAt(map, critter.Home);
                if (gone)
                {
                    critter.Renderer.enabled = false;
                    _critterPool.Add(critter.Renderer);
                    _critters.RemoveAt(i);
                    continue;
                }
                ShowCritter(critter);
            }
        }

        bool LampAt(TileMap map, Vector2 home)
        {
            int x = Mathf.FloorToInt(home.x), y = Mathf.FloorToInt(home.y - 0.4f);
            return map.InBounds(x, y) && _burns[(int)map[x, y]];
        }

        Critter AddCritter(CritterKind kind, Vector2 at, Vector2 home)
        {
            SpriteRenderer renderer;
            if (_critterPool.Count > 0)
            {
                renderer = _critterPool[_critterPool.Count - 1];
                _critterPool.RemoveAt(_critterPool.Count - 1);
                renderer.enabled = true;
            }
            else renderer = Renderer(_root.transform, "Critter", null, Color.white);
            var critter = new Critter { Kind = kind, At = at, Home = home, Renderer = renderer, Phase = UnityEngine.Random.value * 6.28f, Think = UnityEngine.Random.value };
            _critters.Add(critter);
            return critter;
        }

        // Ten times a second: where it means to go.
        void ThinkCritter(TileMap map, Critter critter, Vector2 self)
        {
            var away = critter.At - self;
            float near = away.magnitude;
            critter.Think -= ThinkSeconds;
            switch (critter.Kind)
            {
                case CritterKind.Mouse:
                case CritterKind.SnowHare:
                case CritterKind.Lizard:
                    // Runs from whoever comes within three and a half tiles, fast, until well away. The ice cave's hare
                    // bolts from farther off; the sand cave's lizard lets one come nearer and then darts.
                    if (near < (critter.Kind == CritterKind.SnowHare ? 4.5f : critter.Kind == CritterKind.Lizard ? 2.5f : 3.5f))
                    {
                        critter.Fleeing = true;
                        critter.Heading = away.normalized * (critter.Kind == CritterKind.Lizard ? 6f : 5f);
                        critter.Busy = critter.Kind == CritterKind.Lizard ? 0.6f : 1.2f;
                    }
                    else if (critter.Busy <= 0)
                    {
                        critter.Fleeing = false;
                        if (critter.Think <= 0)
                        {
                            critter.Think = 0.6f + UnityEngine.Random.value * 1.6f;
                            critter.Heading = UnityEngine.Random.value < 0.4f ? Vector2.zero : UnityEngine.Random.insideUnitCircle.normalized * 0.9f;
                        }
                    }
                    break;
                case CritterKind.Cricket:
                    // Sits, and hops: away when someone is near, anywhere otherwise.
                    if (critter.Busy <= 0 && (critter.Think <= 0 || near < 1.6f))
                    {
                        critter.Think = 1 + UnityEngine.Random.value * 2.5f;
                        critter.Heading = (near < 1.6f ? away.normalized : UnityEngine.Random.insideUnitCircle.normalized) * 3.5f;
                        critter.Busy = 0.2f;
                    }
                    else if (critter.Busy <= 0) critter.Heading = Vector2.zero;
                    break;
                case CritterKind.Snail:
                case CritterKind.Scarab:
                    // The scarab rolls its ball a little faster than a snail goes, and turns as seldom.
                    if (critter.Think <= 0)
                    {
                        critter.Think = 3 + UnityEngine.Random.value * 4;
                        critter.Heading = UnityEngine.Random.insideUnitCircle.normalized * (critter.Kind == CritterKind.Scarab ? 0.4f : 0.16f);
                    }
                    break;
                case CritterKind.LampMoth:
                    // Round its lamp, never far from it.
                    var toLamp = critter.Home - critter.At;
                    critter.Heading = new Vector2(-toLamp.y, toLamp.x).normalized * 1.6f + toLamp * (toLamp.magnitude > 0.9f ? 2.5f : 0.3f) + UnityEngine.Random.insideUnitCircle * 0.8f;
                    break;
                default:
                    // Fireflies and spore moths drift about where they came into being.
                    if (critter.Think <= 0)
                    {
                        critter.Think = 0.5f + UnityEngine.Random.value;
                        var home = critter.Home - critter.At;
                        critter.Heading = UnityEngine.Random.insideUnitCircle * (critter.Kind == CritterKind.Firefly ? 0.5f : 1.1f) + (home.sqrMagnitude > 9 ? home.normalized * 0.6f : Vector2.zero);
                    }
                    break;
            }
        }

        void MoveCritter(TileMap map, Critter critter, float deltaTime)
        {
            critter.Busy -= deltaTime;
            var next = critter.At + critter.Heading * deltaTime;
            // What walks keeps to open floor (it turns back at a wall); what flies goes over anything but rock.
            bool flies = CritterArt.Flies(critter.Kind);
            if (flies ? !LightMap.BlocksLight(map[Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y)]) : Walkable(map, next)) critter.At = next;
            else critter.Heading = -critter.Heading;
            if (critter.Heading.x != 0) critter.Left = critter.Heading.x < 0;
            // The snail's trail: a dab of glow every little while, fading.
            if (critter.Kind == CritterKind.Snail && critter.Heading != Vector2.zero && !Settings.ReduceMotion)
            {
                critter.Trail -= deltaTime;
                if (critter.Trail <= 0)
                {
                    critter.Trail = 0.7f;
                    if (_motes.Count < MoteCap) _motes.Add(new Mote { Kind = MoteKind.Trail, At = critter.At, Life = 6 });
                }
            }
        }

        void ShowCritter(Critter critter)
        {
            bool moving = critter.Heading.sqrMagnitude > 0.01f;
            bool flies = CritterArt.Flies(critter.Kind);
            // Wings beat fast; legs step while it moves; a firefly blinks slowly; a sitting thing keeps its first frame.
            float rate = flies ? (critter.Kind == CritterKind.Firefly ? 1.2f : 9f) : critter.Kind == CritterKind.Snail ? 1.5f : critter.Kind == CritterKind.Scarab ? 4f : 8f;
            int frame = flies || moving ? (int)((Time.time + critter.Phase) * rate) % CritterArt.Frames : 0;
            long key = 2L << 40 | (long)critter.Kind << 8 | (long)frame;
            var kind = critter.Kind;
            if (!PropAtlas.TryGet(key, out var sprite)) sprite = PropAtlas.Get(key, () => CritterArt.Image(kind, frame));
            var renderer = critter.Renderer;
            renderer.sprite = sprite;
            renderer.flipX = critter.Left;
            // A cricket's hop; a hare bounds while it runs.
            float bob = flies ? 0.5f + Mathf.Sin((Time.time + critter.Phase) * 3) * 0.08f : critter.Kind == CritterKind.Cricket && critter.Busy > 0 ? 0.18f
                : critter.Kind == CritterKind.SnowHare && moving ? Mathf.Abs(Mathf.Sin((Time.time + critter.Phase) * 9)) * 0.14f : 0;
            renderer.transform.localPosition = new Vector3(critter.At.x, critter.At.y + bob, 0);
            renderer.sortingOrder = CritterArt.Glows(critter.Kind) ? GlowOrder : flies ? FlyOrder : FootOrder(critter.At.y, StandArt.DecorTie);
        }
    }
}
