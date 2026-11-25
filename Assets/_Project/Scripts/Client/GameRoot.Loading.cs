using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The loading screen (Docs/TitleRework.md 4-4, the owner's call of 2026-11-25: a scene of a place the character has been,
    // a tip and a bar). While the world is made a stage at a time (WorldLoader) it shows a small cave of a biome this
    // character has stood in, drawn as the game draws it (LoadingArt), with monsters it has felled there and the hero in
    // its look; under it the stage being made, a bar and one line of help (Tips) that changes every seven seconds or at a
    // tap. Nothing it has not met shows: a new character sees the dirt cave and no monster. Joiners see the same while
    // their map is made.
    public sealed partial class GameRoot
    {
        const float TipSeconds = 7;

        PixelImage _loadImage;
        Biome _loadBiome;
        int _tip = -1;
        float _tipShown;
        GUIStyle _tipStyle;

        // What the loading screen shows, for the playtests: the biome, the monsters, the tip.
        internal string LoadBiome => _loadImage == null ? "" : _loadBiome.ToString();
        internal string LoadMonsters { get; private set; } = "";
        internal string LoadTip => _tip < 0 ? "" : Tips.Text(_tip);

        // Picks the scene for the character about to play: one of the biomes it has been to, the monsters it has felled
        // there (two at most, those of that biome first), its look.
        void PrepareLoading()
        {
            CharacterSummary summary = null;
            byte[] data = SaveStore.Read(SaveStore.CharacterKey(CharacterName));
            if (data != null)
            {
                try
                {
                    summary = CharacterSave.ReadSummary(data, CharacterName);
                }
                catch (InvalidDataException)
                {
                    summary = null;
                }
            }
            var biomes = summary?.VisitedBiomes.ToList() ?? new List<Biome>();
            if (biomes.Count == 0) biomes.Add(Biome.Cave);
            _loadBiome = biomes[Random.Range(0, biomes.Count)];
            var monsters = MetMonsters(summary, _loadBiome);
            LoadMonsters = string.Join(",", monsters.Select(m => m.Id));
            _loadImage = LoadingArt.Scene(_loadBiome, Random.Range(0, 10000), monsters, summary?.Look ?? NewLook);
            _tip = Tips.Next(_tip);
            _tipShown = Time.unscaledTime;
        }

        // Up to two monsters the character has felled that live in that biome (or anywhere, in the dirt cave), with their
        // sheet colours; none for one that has felled none.
        List<(string Id, Rgba Color)> MetMonsters(CharacterSummary summary, Biome biome)
        {
            var shown = new List<(string, Rgba)>();
            if (summary == null || _monsters == null) return shown;
            foreach (bool anywhere in new[] { false, true })
            {
                foreach (string id in summary.Monsters)
                {
                    if (shown.Count >= 2 || shown.Any(s => s.Item1 == id)) continue;
                    var monster = _monsters.Find(id);
                    if (monster == null || monster.Big) continue;
                    bool here = anywhere ? monster.Biome == null && biome == Biome.Cave : monster.Biome == biome;
                    if (!here) continue;
                    shown.Add((id, string.IsNullOrEmpty(monster.Color) ? Rgba.Hex("#8CD94D") : Rgba.Hex(monster.Color)));
                }
            }
            return shown;
        }

        void DrawLoading()
        {
            var screen = new Rect(0, 0, Ui.Width, Ui.Height);
            if (Event.current.type == EventType.Repaint)
            {
                var previous = GUI.color;
                GUI.color = new Color(0.03f, 0.025f, 0.035f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = previous;
            }
            if (_loadImage == null) PrepareLoading();
            if (Time.unscaledTime - _tipShown > TipSeconds)
            {
                _tip = Tips.Next(_tip);
                _tipShown = Time.unscaledTime;
            }

            // The picture as big as fits with room under it for the words, at whole device pixels a pixel.
            float room = Ui.Short ? 96 : 130;
            float wanted = Mathf.Min(Ui.Width - 40, 720, (Ui.Height - room - 40) * LoadingArt.Width / LoadingArt.Height);
            float pixel = Mathf.Max(1, Mathf.Floor(wanted * Ui.Scale / LoadingArt.Width)) / Ui.Scale;
            var size = new Vector2(LoadingArt.Width * pixel, LoadingArt.Height * pixel);
            float top = Mathf.Max(12, Mathf.Round((Ui.Height - size.y - room) / 2));
            var picture = new Rect(Mathf.Round((Ui.Width - size.x) / 2), top, size.x, size.y);
            float frame = TitleArt.PanelBorder * UiSkin.Pt;
            TitleDraw.Panel(new Rect(picture.x - frame, picture.y - frame, picture.width + 2 * frame, picture.height + 2 * frame));
            TitleDraw.Picture(picture.position, _loadImage, pixel);
            TitleDraw.Text(new Rect(picture.x + 8, picture.yMax - 24, picture.width - 16, 20), TitleDraw.BiomeName(_loadBiome), 12, TitleDraw.Faint);

            // The stage, the bar, the tip.
            float width = Mathf.Max(picture.width, Mathf.Min(Ui.Width - 32, 300));
            float x = Mathf.Round((Ui.Width - width) / 2);
            float y = picture.yMax + frame + 8;
            TitleDraw.Text(new Rect(x, y, width, 22), LoadText(LoadStage), 13, TitleDraw.GoldInk, TextAnchor.MiddleCenter);
            y += 24;
            DrawLoadBar(new Rect(x, y, width, 14), LoadFraction);
            y += 22;
            _tipStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = false };
            Ui.Text(_tipStyle, 13);
            string tip = Ui.Wrap(LoadTip, _tipStyle, width);
            float tipHeight = _tipStyle.CalcHeight(new GUIContent(tip), width);
            if (Event.current.type == EventType.Repaint)
            {
                _tipStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
                GUI.Label(new Rect(x + 1, y + 1, width, tipHeight), tip, _tipStyle);
                _tipStyle.normal.textColor = TitleDraw.Ink;
                GUI.Label(new Rect(x, y, width, tipHeight), tip, _tipStyle);
            }
            // A tap or click anywhere shows the next tip.
            if (Ui.Invisible(screen, Strings.T("load.next_tip", "다음 도움말")))
            {
                _tip = Tips.Next(_tip);
                _tipShown = Time.unscaledTime;
            }
        }

        // A thin bar in the title's look: a dark band, gold filling it, a bright edge where the gold ends.
        static void DrawLoadBar(Rect bar, float share)
        {
            if (Event.current.type != EventType.Repaint) return;
            TitleDraw.Row(bar, TitleState.Normal);
            float inset = 3;
            var inner = new Rect(bar.x + inset, bar.y + inset, bar.width - 2 * inset, bar.height - 2 * inset);
            var fill = new Rect(inner.x, inner.y, Mathf.Round(inner.width * Mathf.Clamp01(share)), inner.height);
            var previous = GUI.color;
            GUI.color = new Color(0.77f, 0.6f, 0.33f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = new Color(0.96f, 0.87f, 0.61f);
            GUI.DrawTexture(new Rect(fill.x, fill.y, fill.width, 1), Texture2D.whiteTexture);
            if (fill.width > 2) GUI.DrawTexture(new Rect(fill.xMax - 2, fill.y, 2, fill.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
