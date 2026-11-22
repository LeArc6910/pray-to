using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The passive tree (P, Docs/UiRework.md 4-3 특성 트리, U4; Docs/Classes.md 패시브 트리) over the whole screen on the
    // night sky, the one screen meant to look unlike the rest (the person's pick of 2026-11-22), as Path of Exile, Last
    // Epoch and Grim Dawn lay a tree out:
    //  - the points as stars and runes (GrowthArt.Node): a learned one a lit orb in gold, one that can be learned with an
    //    outline that breathes, the rest dim; a big point (notable) larger with a second ring; on each a glyph of what it
    //    raises in its kind's colour (attack red, defence green, magic blue, the rest gold); the lines between two learned
    //    points lit gold, a way that can be taken half lit. A point just learned flashes and light runs along its lines.
    //  - dragged with the mouse or a finger, zoomed by the wheel (round the pointer), two fingers or [+]/[-]; names under
    //    the points once zoomed in; [◇] back to the middle. It opens on the last point learned (the start before any),
    //    zoomed so a finger's 40 points press a point on a touch screen.
    //  - the picked point's card with what it raises and [배우기] (pressing a picked point that can be learned again
    //    learns it too), the points to spend, [모두 되돌리기 (금화 N)] in red, and "지금 배울 수 있는 점" as a drawer of
    //    big buttons, for fingers that would rather not aim. A panel at the right on a PC and a phone held sideways; on a
    //    phone held upright the points in a corner, the card and the drawer at the bottom over the sky.
    sealed partial class InventoryWindow
    {
        PassiveNode _pickedPassive;
        // The tree opens afresh (centred and zoomed) when the screen is shown again.
        bool _treeFresh = true;
        float _treeZoom = 48;
        Vector2 _treePan;
        bool _drawerOpen = true;
        Vector2 _treeSideScroll, _drawerScroll;
        // A drag of the sky (mouse or finger) and a pinch of two fingers.
        bool _treeArmed, _treeDragging;
        Vector2 _treeFrom, _treePanFrom;
        bool _pinching;
        float _pinchFrom, _pinchZoom;
        Vector2 _pinchMid, _pinchPan;
        // When each point was learned while the tree was up, for its flash; the points known learned.
        readonly Dictionary<PassiveNode, float> _learnedAt = new Dictionary<PassiveNode, float>();
        HashSet<PassiveNode> _treeKnown;
        // Where the sky was last drawn (the playtests drag and pinch it there).
        Rect _treeSky;

        const float MostZoom = 110;

        // Where the tree screen's parts go in the area under the tabs (also for the play mode layout tests): the sky, the
        // panel beside it (PC, a phone held sideways) or, without one, the points chip, [모두 되돌리기], the picked point's
        // card and the drawer over the sky; the zoom buttons in the sky's top right corner.
        internal readonly struct TreeLayout
        {
            public TreeLayout(Rect view, Rect side, Rect zoom, Rect points, Rect reset, Rect card, Rect drawer)
            {
                View = view;
                Side = side;
                Zoom = zoom;
                Points = points;
                Reset = reset;
                Card = card;
                Drawer = drawer;
            }

            public Rect View { get; }
            public Rect Side { get; }
            public bool HasSide => Side.width > 0;
            public Rect Zoom { get; }
            public Rect Points { get; }
            public Rect Reset { get; }
            public Rect Card { get; }
            public Rect Drawer { get; }
        }

        internal static TreeLayout LayTree(Rect body, bool drawerOpen, int drawerRows, float cardHeight)
        {
            float button = GrowthButton;
            float zoomWidth = 3 * button + 8;
            if (body.width >= 640)
            {
                float width = Mathf.Clamp(Mathf.Round(body.width * 0.3f), 250, 330);
                var side = Rect.MinMaxRect(body.xMax - width, body.y, body.xMax, body.yMax);
                var view = Rect.MinMaxRect(body.x, body.y, side.x - 8, body.yMax);
                return new TreeLayout(view, side, new Rect(view.xMax - 8 - zoomWidth, view.y + 8, zoomWidth, button), default, default, default, default);
            }
            var sky = body;
            var points = new Rect(sky.x + 6, sky.y + 6, 164, 44);
            var zoom = new Rect(sky.xMax - 6 - zoomWidth, sky.y + 6, zoomWidth, button);
            var reset = new Rect(sky.x + 6, points.yMax + 6, 200, button);
            // Three rows at most: the sky stays the larger part (more scroll in the drawer).
            float drawerHeight = button + 8 + (drawerOpen && drawerRows > 0 ? Mathf.Min(drawerRows, 3) * (button + 4) + 4 : 0);
            var drawer = new Rect(sky.x + 6, sky.yMax - 6 - drawerHeight, sky.width - 12, drawerHeight);
            var card = cardHeight > 0 ? new Rect(sky.x + 6, drawer.y - 6 - cardHeight, sky.width - 12, cardHeight) : default;
            return new TreeLayout(sky, default, zoom, points, reset, card, drawer);
        }

        void DrawTreeScreen(Rect body)
        {
            var tree = _client.PassiveTree;
            if (tree.Count == 0)
            {
                Write(new Rect(body.x + 10, body.y + 10, body.width - 20, 40), "특성 트리 시트가 없다. 명령 한 줄로 프로젝트를 다시 설정한다.", Ink, wrap: true, shadow: true);
                return;
            }
            if (_pickedPassive != null && !tree.Contains(_pickedPassive)) _pickedPassive = null;
            var open = tree.Where(_client.CanAllocate).ToList();
            bool side = body.width >= 640;
            float cardHeight = !side && _pickedPassive != null ? NodeCardHeight(_pickedPassive, body.width - 12) : 0;
            var lay = LayTree(body, _drawerOpen, open.Count, cardHeight);
            NoteLearned();

            var covers = new List<Rect> { lay.Zoom };
            if (!lay.HasSide)
            {
                covers.Add(lay.Points);
                if (_client.Passives.Count > 0) covers.Add(lay.Reset);
                covers.Add(lay.Drawer);
                if (cardHeight > 0) covers.Add(lay.Card);
                if (_drawerOpen && open.Count > 0) Ui.TouchScroll(DrawerList(lay.Drawer), ref _drawerScroll);
            }
            else
            {
                Ui.TouchScroll(lay.Side, ref _treeSideScroll);
            }
            _treeSky = lay.View;
            DrawTree(lay.View, tree, covers);
            DrawZoomButtons(lay.View, lay.Zoom, tree);
            if (lay.HasSide) DrawTreeSide(lay.Side, open);
            else DrawTreeOverlays(lay, open);
        }

        // ---- The sky ----

        static float FitZoom(Rect view, IReadOnlyList<PassiveNode> tree)
        {
            int minX = tree.Min(n => n.X), maxX = tree.Max(n => n.X), minY = tree.Min(n => n.Y), maxY = tree.Max(n => n.Y);
            return Mathf.Min((view.width - 48) / Mathf.Max(1, maxX - minX), (view.height - 48) / Mathf.Max(1, maxY - minY));
        }

        static float LeastZoom(Rect view, IReadOnlyList<PassiveNode> tree) => Mathf.Max(14, Mathf.Min(FitZoom(view, tree), 40) * 0.85f);

        // A finger's 40 points round every point on a touch screen (4-3: 그만큼 확대된 채로 연다); the whole tree on a PC.
        static float StartZoom(Rect view, IReadOnlyList<PassiveNode> tree)
        {
            float fit = FitZoom(view, tree);
            return Controls.UsingTouch ? Mathf.Clamp(Mathf.Max(fit, 46), 20, MostZoom) : Mathf.Clamp(fit, 30, 72);
        }

        // The pan kept so the tree never leaves the sky: its middle within reach of the view's middle.
        void ClampPan(Rect view, IReadOnlyList<PassiveNode> tree)
        {
            int minX = tree.Min(n => n.X), maxX = tree.Max(n => n.X), minY = tree.Min(n => n.Y), maxY = tree.Max(n => n.Y);
            float z = _treeZoom;
            _treePan.x = Mathf.Clamp(_treePan.x, -maxX * z - view.width * 0.4f, -minX * z + view.width * 0.4f);
            _treePan.y = Mathf.Clamp(_treePan.y, -maxY * z - view.height * 0.4f, -minY * z + view.height * 0.4f);
        }

        void ZoomAt(Rect view, IReadOnlyList<PassiveNode> tree, Vector2 point, float zoom)
        {
            var center = view.center;
            var at = (point - center - _treePan) / _treeZoom;
            _treeZoom = Mathf.Clamp(zoom, LeastZoom(view, tree), MostZoom);
            _treePan = point - center - at * _treeZoom;
            ClampPan(view, tree);
        }

        void CenterOn(PassiveNode node) => _treePan = -new Vector2(node.X, node.Y) * _treeZoom;

        // The points learned since the tree was last seen flash (4-6 특성 배우기); none when it opens.
        void NoteLearned()
        {
            if (Event.current.type != EventType.Repaint) return;
            var taken = _client.Passives;
            if (_treeKnown == null || taken.Count < _treeKnown.Count)
            {
                _treeKnown = new HashSet<PassiveNode>(taken);
                return;
            }
            foreach (var node in taken)
                if (_treeKnown.Add(node)) _learnedAt[node] = Time.unscaledTime;
        }

        bool Taken(PassiveNode node) => node.IsStart || _client.Passives.Contains(node);

        void DrawTree(Rect view, IReadOnlyList<PassiveNode> tree, List<Rect> covers)
        {
            var e = Event.current;
            if (_treeFresh)
            {
                _treeFresh = false;
                _treeZoom = StartZoom(view, tree);
                var start = _client.Passives.Count > 0 ? _client.Passives[_client.Passives.Count - 1] : tree.FirstOrDefault(n => n.IsStart) ?? tree[0];
                CenterOn(start);
                ClampPan(view, tree);
                _treeKnown = null;
                _treeArmed = _treeDragging = _pinching = false;
            }
            bool Covered(Vector2 point) => covers.Any(c => c.Contains(point));

            // Two fingers: zoom round their middle and follow it (once a frame).
            if (e.type == EventType.Layout) Pinch(view, tree);
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition) && !Covered(e.mousePosition))
            {
                float notches = Mathf.Clamp(e.delta.y, -3, 3);
                ZoomAt(view, tree, e.mousePosition, _treeZoom * Mathf.Pow(1.15f, -Mathf.Sign(notches) * Mathf.Max(1, Mathf.Abs(notches) / 3 * 2)));
                e.Use();
            }
            // A drag of the sky pans it; once it has moved a few points the press is no longer a press of a point.
            switch (e.type)
            {
                case EventType.MouseDown:
                    _treeArmed = view.Contains(e.mousePosition) && !Covered(e.mousePosition) && !_pinching;
                    _treeDragging = false;
                    _treeFrom = e.mousePosition;
                    _treePanFrom = _treePan;
                    break;
                case EventType.MouseDrag:
                    if (!_treeArmed || _pinching || Controls.FingerCount >= 2) break;
                    if (!_treeDragging && (e.mousePosition - _treeFrom).magnitude > 6)
                    {
                        _treeDragging = true;
                        GUIUtility.hotControl = 0;
                    }
                    if (_treeDragging)
                    {
                        _treePan = _treePanFrom + (e.mousePosition - _treeFrom);
                        ClampPan(view, tree);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (_treeDragging) e.Use();
                    break;
            }

            float z = _treeZoom;
            int tier = z < 36 ? 0 : z < 58 ? 1 : 2;
            float pt = UiSkin.Pt;
            bool touch = Controls.UsingTouch;
            GUI.BeginGroup(view);
            var local = new Rect(0, 0, view.width, view.height);
            Vector2 At(PassiveNode n) => local.center + _treePan + new Vector2(n.X, n.Y) * z;
            float now = Time.unscaledTime;

            // The lines: lit gold between two learned points, half lit where a way can be taken, faint otherwise.
            if (e.type == EventType.Repaint)
            {
                foreach (var node in tree)
                {
                    foreach (var other in node.Links)
                    {
                        if (string.CompareOrdinal(other.Id, node.Id) <= 0) continue;
                        var a = At(node);
                        var b = At(other);
                        bool ta = Taken(node), tb = Taken(other);
                        if (ta && tb)
                        {
                            Line(a, b, 8, new Color(1f, 0.8f, 0.35f, 0.18f));
                            Line(a, b, 3, new Color(1f, 0.86f, 0.5f, 0.95f));
                        }
                        else if ((ta || tb) && (_client.CanAllocate(node) || _client.CanAllocate(other)))
                            Line(a, b, 2, new Color(1f, 0.82f, 0.45f, 0.5f));
                        else
                            Line(a, b, 2, new Color(0.45f, 0.52f, 0.85f, 0.35f));
                        // Light running along the lines of a point just learned, from it to its learned neighbour.
                        foreach (var (from, to) in new[] { (node, other), (other, node) })
                        {
                            if (UiMotion.Reduced || !_learnedAt.TryGetValue(from, out float when) || !Taken(to)) continue;
                            float t = (now - when) / 0.6f;
                            if (t < 0 || t > 1) continue;
                            var spot = Vector2.Lerp(At(from), At(to), t);
                            Fill(new Rect(spot.x - 3, spot.y - 3, 6, 6), new Color(1f, 0.95f, 0.75f, 1 - t));
                        }
                    }
                }
            }

            Ui.Row("특성 트리");
            PassiveNode hovered = null;
            foreach (var node in tree)
            {
                var p = At(node);
                var look = node.IsStart ? GrowthArt.NodeLook.Start : Taken(node) ? GrowthArt.NodeLook.Taken : _client.CanAllocate(node) ? GrowthArt.NodeLook.Open : GrowthArt.NodeLook.Locked;
                int radius = node.IsStart ? 5 + tier : node.Notable ? 6 + 2 * tier : 4 + tier;
                var color = GrowthArt.KindOf(node);
                var image = GrowthArt.Node(radius, look, color, node.Notable);
                float size = image.Width * pt;
                float circle = (2 * radius + 1) * pt;
                if (p.x < -size || p.y < -size || p.x > local.width + size || p.y > local.height + size) continue;
                if (e.type == EventType.Repaint)
                {
                    UiSkin.Image(new Vector2(p.x - size / 2, p.y - size / 2), image);
                    if (look == GrowthArt.NodeLook.Open)
                    {
                        // The outline of a point that can be learned breathes (still with less motion).
                        float breath = UiMotion.Reduced ? 0.8f : 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(now * 4));
                        var previous = GUI.color;
                        GUI.color = new Color(1, 1, 1, previous.a * breath);
                        UiSkin.Image(new Vector2(p.x - size / 2, p.y - size / 2), GrowthArt.Ring(radius, color.Mix(Rgba.White, 0.5f), radius + 1));
                        GUI.color = previous;
                    }
                    var glyph = GrowthArt.GlyphOf(node);
                    if (glyph != null && (tier > 0 || node.Notable))
                    {
                        var glyphColor = GrowthArt.GlyphColor(node);
                        if (look == GrowthArt.NodeLook.Locked) glyphColor = glyphColor.Mix(GrowthArt.Faint, 0.55f);
                        if (look == GrowthArt.NodeLook.Taken) glyphColor = glyphColor.Mix(Rgba.White, 0.35f);
                        var glyphSize = UiSkin.IconSize(glyph);
                        UiSkin.Icon(new Rect(p.x - glyphSize.x / 2, p.y - glyphSize.y / 2, glyphSize.x, glyphSize.y), glyph, ToColor(glyphColor));
                    }
                    if (node == _pickedPassive)
                        UiSkin.Image(new Vector2(p.x - size / 2, p.y - size / 2), GrowthArt.Ring(radius, Rgba.White, radius + GrowthArt.Margin));
                    // Just learned: a ring that grows and fades.
                    if (!UiMotion.Reduced && _learnedAt.TryGetValue(node, out float learned) && now - learned < 0.6f)
                    {
                        float t = (now - learned) / 0.6f;
                        int ring = radius + 1 + Mathf.RoundToInt(t * 7);
                        var flash = GrowthArt.Ring(ring, GrowthArt.Gold, ring);
                        float flashSize = flash.Width * pt;
                        var previous = GUI.color;
                        GUI.color = new Color(1, 1, 1, previous.a * (1 - t));
                        UiSkin.Image(new Vector2(p.x - flashSize / 2, p.y - flashSize / 2), flash);
                        GUI.color = previous;
                    }
                    // Names under the points once zoomed in (the big ones sooner).
                    if (z >= 70 || node.Notable && z >= 48 || node == _pickedPassive)
                    {
                        var ink = look == GrowthArt.NodeLook.Taken ? new Color(1f, 0.88f, 0.55f) : look == GrowthArt.NodeLook.Open ? Ink : new Color(0.62f, 0.66f, 0.85f);
                        Write(new Rect(p.x - 60, p.y + circle / 2 + 3, 120, 14), node.Name, ink, align: TextAnchor.UpperCenter, shadow: true);
                    }
                }
                float side = Mathf.Max(circle, touch ? 40 : 22);
                var hit = new Rect(p.x - side / 2, p.y - side / 2, side, side);
                // Only a point wholly in the sky and clear of what lies over it is a button (and known to the playtests).
                if (!local.Contains(hit.min) || !local.Contains(hit.max)) continue;
                if (Covered(new Vector2(hit.center.x + view.x, hit.center.y + view.y))) continue;
                Ui.Probe(node.Name, hit);
                if (hit.Contains(e.mousePosition) && !touch && !_treeDragging) hovered = node;
                if (!GUI.Button(hit, GUIContent.none, GUIStyle.none)) continue;
                UiMotion.Confirm();
                if (node.IsStart) _pickedPassive = null;
                else if (_pickedPassive == node && _client.CanAllocate(node)) Send(_client.AllocatePassiveCommand(node), Sound.LevelUp);
                else _pickedPassive = node;
            }
            GUI.EndGroup();

            // A press of the sky itself (no drag, no point) lets the picked point go.
            if (e.rawType == EventType.MouseUp)
            {
                if (e.type == EventType.MouseUp && _treeArmed && !_treeDragging && view.Contains(e.mousePosition) && !Covered(e.mousePosition)) _pickedPassive = null;
                _treeArmed = _treeDragging = false;
            }
            // On a PC the point under the mouse is told on a card beside it.
            if (hovered != null && e.type == EventType.Repaint)
            {
                var size = Ui.CardSize(NodeText(hovered), 260, out string wrapped);
                var pointer = e.mousePosition;
                float x = pointer.x + 18 + size.x > Ui.Width - 8 ? pointer.x - 18 - size.x : pointer.x + 18;
                float y = Mathf.Clamp(pointer.y - 12, 8, Mathf.Max(8, Ui.Height - size.y - 8));
                Ui.LateCard(new Rect(x, y, size.x, size.y), wrapped);
            }
        }

        // Two fingers on the sky: the tree zooms by how far apart they are and its point under their middle stays there.
        void Pinch(Rect view, IReadOnlyList<PassiveNode> tree)
        {
            if (Controls.FingerCount < 2)
            {
                _pinching = false;
                return;
            }
            var a = Controls.FingerAt(0);
            var b = Controls.FingerAt(1);
            float apart = Mathf.Max(1, Vector2.Distance(a, b));
            var middle = (a + b) / 2;
            if (!_pinching)
            {
                if (!view.Contains(middle)) return;
                _pinching = true;
                _pinchFrom = apart;
                _pinchZoom = _treeZoom;
                _pinchMid = middle;
                _pinchPan = _treePan;
                _treeArmed = _treeDragging = false;
                GUIUtility.hotControl = 0;
                return;
            }
            var center = view.center;
            var at = (_pinchMid - center - _pinchPan) / _pinchZoom;
            _treeZoom = Mathf.Clamp(_pinchZoom * apart / _pinchFrom, LeastZoom(view, tree), MostZoom);
            _treePan = middle - center - at * _treeZoom;
            ClampPan(view, tree);
        }

        // A straight line of that width between two points, turned with the GUI matrix.
        static void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a;
            float length = d.magnitude;
            if (length < 0.5f) return;
            var matrix = GUI.matrix;
            var previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * previous.a);
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2, length, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }

        // [-] [+] [◇]: zoom out, in, and back to the last point learned.
        void DrawZoomButtons(Rect view, Rect strip, IReadOnlyList<PassiveNode> tree)
        {
            float button = strip.height;
            Ui.Row("특성 트리");
            if (Ui.Button(new Rect(strip.x, strip.y, button, button), "-", "축소")) ZoomAt(view, tree, view.center, _treeZoom / 1.25f);
            if (Ui.Button(new Rect(strip.x + button + 4, strip.y, button, button), "+", "확대")) ZoomAt(view, tree, view.center, _treeZoom * 1.25f);
            if (Ui.Button(new Rect(strip.x + 2 * (button + 4), strip.y, button, button), "◇", "가운데로"))
            {
                _treeZoom = StartZoom(view, tree);
                CenterOn(_client.Passives.Count > 0 ? _client.Passives[_client.Passives.Count - 1] : tree.FirstOrDefault(n => n.IsStart) ?? tree[0]);
                ClampPan(view, tree);
            }
        }

        // ---- The point's card, the points, the drawer ----

        string NodeState(PassiveNode node) =>
            node.IsStart ? "모든 갈래가 여기서 시작한다"
            : _client.Passives.Contains(node) ? "배웠다"
            : _client.CanAllocate(node) ? (node == _pickedPassive ? "배울 수 있다 · 한 번 더 누르면 배운다" : "배울 수 있다(포인트 1)")
            : _client.PassivePoints == 0 ? "특성 포인트가 없다(레벨마다 1)"
            : "이어진 점을 먼저 배운다";

        static string NodeKind(PassiveNode node) => node.IsStart ? "시작점" : $"{GrowthArt.KindName(node)} · {(node.Notable ? "큰 점" : "작은 점")}";

        static IEnumerable<string> NodeStats(PassiveNode node) => node.Modifiers.Select(StatText.Describe);

        string NodeText(PassiveNode node) => $"{node.Name} ({NodeKind(node)})\n{string.Join("\n", NodeStats(node))}\n{NodeState(node)}";

        float NodeCardHeight(PassiveNode node, float width)
        {
            float text = width - 20;
            return 8 + 20 + 16 + NodeStats(node).Sum(s => TextHeight(s, text)) + 4 + TextHeight(NodeState(node), text) + 6 + GrowthButton + 8;
        }

        // The picked point: its name and kind, what it raises in green, whether it can be learned, and [배우기].
        void DrawNodeCard(Rect r, PassiveNode node)
        {
            var color = ToColor(GrowthArt.KindOf(node));
            if (Event.current.type == EventType.Repaint)
            {
                UiSkin.Card(r);
                Fill(new Rect(r.x + 3, r.y + 3, 3, r.height - 6), color);
            }
            float x = r.x + 12, width = r.width - 20;
            float y = r.y + 8;
            Write(new Rect(x, y, width, 18), node.Name, new Color(1f, 0.86f, 0.45f));
            y += 20;
            Write(new Rect(x, y, width, 14), NodeKind(node), color);
            y += 16;
            foreach (string stat in NodeStats(node))
            {
                float h = TextHeight(stat, width);
                Write(new Rect(x, y, width, h), stat, Good, wrap: true);
                y += h;
            }
            y += 4;
            string state = NodeState(node);
            float stateHeight = TextHeight(state, width);
            Write(new Rect(x, y, width, stateHeight), state, _client.CanAllocate(node) ? new Color(1f, 0.85f, 0.45f) : UiSkin.Muted, wrap: true);
            y += stateHeight + 6;
            if (node.IsStart) return;
            bool taken = _client.Passives.Contains(node);
            Ui.Row(node.Name);
            GUI.enabled = _client.CanAllocate(node);
            if (Ui.Button(new Rect(x, y, Mathf.Min(140, width), GrowthButton), taken ? "배움" : "배우기", Ui.Look.Primary))
                Send(_client.AllocatePassiveCommand(node), Sound.LevelUp);
            GUI.enabled = true;
        }

        // The points to spend in large gold, and how many of the tree are learned.
        void DrawTreePoints(Rect r, bool chip)
        {
            if (chip && Event.current.type == EventType.Repaint) UiSkin.Card(r);
            int points = _client.PassivePoints;
            var gold = points > 0 ? new Color(1f, 0.84f, 0.35f) : UiSkin.Muted;
            var gem = UiSkin.IconSize(GrowthArt.Point);
            float x = r.x + (chip ? 10 : 2);
            UiSkin.Icon(new Rect(x, r.center.y - gem.y / 2, gem.x, gem.y), GrowthArt.Point, points > 0 ? UiSkin.Gold : UiSkin.Muted);
            x += gem.x + 6;
            string count = points.ToString();
            float width = Style(24, gold).CalcSize(new GUIContent(count)).x;
            Write(new Rect(x, r.y, width + 4, r.height), count, gold, 24, shadow: true);
            x += width + 8;
            int all = _client.PassiveTree.Count(n => !n.IsStart);
            Write(Rect.MinMaxRect(x, r.y + 4, r.xMax - 4, r.yMax - 4), $"특성 포인트 {points}\n배운 점 {_client.Passives.Count}/{all}", Ink, wrap: true, shadow: true);
        }

        void ResetButton(Rect r)
        {
            if (_client.Passives.Count == 0) return;
            // A few coins a point (World.ResetPassives).
            int resetCost = _client.Passives.Count * ClassRules.PassiveResetCoinsPerPoint;
            var gold = _client.Loot.FindMaterial(World.Coin);
            Ui.Row("특성 트리");
            GUI.enabled = gold != null && _client.Bag.CountOf(gold) >= resetCost;
            if (Ui.Button(r, $"모두 되돌리기 (금화 {resetCost})", Ui.Look.Danger)) Send(_client.ResetPassivesCommand(), Sound.Click);
            GUI.enabled = true;
        }

        // The drawer's head: "지금 배울 수 있는 점 N" with ▼ open, ▲ shut; pressing it opens or shuts it.
        void DrawerHead(Rect r, int count)
        {
            Ui.Row("특성 트리");
            if (Ui.Button(r, $"지금 배울 수 있는 점 {count} {(_drawerOpen ? "▼" : "▲")}", "지금 배울 수 있는 점", count > 0 ? Ui.Look.Primary : Ui.Look.Normal))
                _drawerOpen = !_drawerOpen;
        }

        // The points that can be taken now as big buttons ("이름: 올리는 것"): pressing one picks it and brings it to the
        // middle of the sky, then [배우기].
        void DrawerItems(List<PassiveNode> open)
        {
            if (open.Count == 0)
            {
                GUILayout.Label(Fit(_client.PassivePoints == 0 ? "특성 포인트가 없다. 레벨이 오를 때마다 1" : "배울 수 있는 점이 없다", _textWidth));
                return;
            }
            foreach (var node in open)
            {
                Ui.Row("지금 배울 수 있는 점");
                if (WrapButton($"{node.Name}: {string.Join(", ", NodeStats(node))}", _textWidth, node == _pickedPassive ? Ui.Look.Primary : Ui.Look.Normal))
                {
                    _pickedPassive = node;
                    CenterOn(node);
                    // The picked point's card, with its [배우기], is at the top of the side pane: on a short screen
                    // the drawer sits below it, scrolled, so the card would stay out of sight.
                    _treeSideScroll = Vector2.zero;
                }
                GUILayout.Space(4);
            }
        }

        // The panel beside the sky (a PC, a phone held sideways): the points, the picked point's card, [모두 되돌리기] and
        // the drawer, scrolling together when they do not fit.
        void DrawTreeSide(Rect side, List<PassiveNode> open)
        {
            if (Event.current.type == EventType.Repaint)
            {
                UiSkin.Inset(side);
                Fill(new Rect(side.x + 2, side.y + 2, side.width - 4, side.height - 4), new Color(0.03f, 0.04f, 0.1f, 0.55f));
            }
            var inner = Rect.MinMaxRect(side.x + 4, side.y + 4, side.xMax - 2, side.yMax - 4);
            _treeSideScroll = Pane(inner, _treeSideScroll, () =>
            {
                DrawTreePoints(Room(48), false);
                GUILayout.Label(Fit("시작점이나 배운 점과 이어진 점을 배운다. 끌어서 옮기고 휠로 확대한다", _textWidth));
                GUILayout.Space(6);
                if (_pickedPassive != null) DrawNodeCard(Room(NodeCardHeight(_pickedPassive, _textWidth)), _pickedPassive);
                else GUILayout.Label(Fit("점을 누르면 여기에 설명이 나온다. 한 번 더 누르면 배운다", _textWidth));
                GUILayout.Space(8);
                if (_client.Passives.Count > 0)
                {
                    var reset = Room(GrowthButton);
                    ResetButton(new Rect(reset.x, reset.y, Mathf.Min(reset.width, 220), reset.height));
                    GUILayout.Space(8);
                }
                DrawerHead(Room(GrowthButton), open.Count);
                GUILayout.Space(4);
                if (_drawerOpen) DrawerItems(open);
            });
        }

        // The drawer's list under its head.
        static Rect DrawerList(Rect drawer) => Rect.MinMaxRect(drawer.x + 4, drawer.y + GrowthButton + 8, drawer.xMax - 2, drawer.yMax - 4);

        // A phone held upright: the points in the top left with [모두 되돌리기] under them, the picked point's card and the
        // drawer at the bottom, all over the sky.
        void DrawTreeOverlays(TreeLayout lay, List<PassiveNode> open)
        {
            DrawTreePoints(lay.Points, true);
            ResetButton(lay.Reset);
            if (_pickedPassive != null && lay.Card.height > 0) DrawNodeCard(lay.Card, _pickedPassive);
            if (Event.current.type == EventType.Repaint) UiSkin.Card(lay.Drawer);
            DrawerHead(new Rect(lay.Drawer.x + 4, lay.Drawer.y + 4, lay.Drawer.width - 8, GrowthButton), open.Count);
            if (_drawerOpen && open.Count > 0) _drawerScroll = Pane(DrawerList(lay.Drawer), _drawerScroll, () => DrawerItems(open));
        }
    }
}
