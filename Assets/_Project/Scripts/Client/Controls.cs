using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    // Keyboard and mouse: WASD or arrow keys to move, the mouse to aim, left button or space to attack (held to keep
    // swinging). Touch: drag on the left half of the screen to move (the stick appears where the finger lands), press
    // and hold on the right half to attack towards that point. Touches that start on a UI button or window, which
    // OnGUI marks with Block, are left to the UI.
    static class Controls
    {
        // How far the stick can be pulled, in UI points, and the share of that which still counts as standing still.
        const float StickRadius = 60;
        const float DeadZone = 0.15f;
        // Browsers also report taps as mouse clicks, so the mouse is ignored for a moment after any touch.
        const float MouseQuietSeconds = 1;

        static readonly List<(int Id, Vector2 Pixels)> _touches = new List<(int, Vector2)>();
        static readonly HashSet<int> _ignored = new HashSet<int>();
        static readonly List<Rect> _blocked = new List<Rect>();
        static readonly Predicate<int> Released = id => !Held(id);
        static int _stickFinger = -1;
        static Vector2 _stickOrigin;
        static Vector2 _stickNow;
        static int _attackFinger = -1;
        static Vector2 _attackPixels;
        static float _lastTouch = float.NegativeInfinity;

        // A touch has been seen this run: the HUD shows touch buttons and hints.
        public static bool UsingTouch { get; private set; }

        // Once per frame, before Read: follows the fingers. Then drops last frame's UI areas; OnGUI marks them again.
        public static void UpdateTouches()
        {
            _touches.Clear();
            var screen = Touchscreen.current;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (touch.press.isPressed) _touches.Add((touch.touchId.ReadValue(), touch.position.ReadValue()));
                }
            }
            if (_touches.Count > 0)
            {
                _lastTouch = Time.unscaledTime;
                UsingTouch = true;
            }

            if (!Held(_stickFinger)) _stickFinger = -1;
            if (!Held(_attackFinger)) _attackFinger = -1;
            _ignored.RemoveWhere(Released);
            foreach (var (id, pixels) in _touches)
            {
                if (id == _stickFinger) _stickNow = Ui.FromScreen(pixels);
                else if (id == _attackFinger) _attackPixels = pixels;
                else if (!_ignored.Contains(id)) Begin(id, pixels);
            }
            _blocked.Clear();
        }

        // Marks a UI area, in points, where new touches belong to the UI. Call from OnGUI.
        public static void Block(Rect area)
        {
            if (Event.current.type == EventType.Repaint) _blocked.Add(area);
        }

        public static (Vec2 Move, Vec2 Aim, bool Attack) Read(Camera camera, Vec2 player)
        {
            var keyboard = Keyboard.current;
            var mouse = Time.unscaledTime - _lastTouch < MouseQuietSeconds ? null : Mouse.current;

            float x = 0;
            float y = 0;
            if (keyboard != null)
            {
                if (keyboard[Key.A].isPressed || keyboard[Key.LeftArrow].isPressed) x -= 1;
                if (keyboard[Key.D].isPressed || keyboard[Key.RightArrow].isPressed) x += 1;
                if (keyboard[Key.S].isPressed || keyboard[Key.DownArrow].isPressed) y -= 1;
                if (keyboard[Key.W].isPressed || keyboard[Key.UpArrow].isPressed) y += 1;
            }
            var move = new Vec2(x, y).Normalized;
            if (_stickFinger >= 0)
            {
                // GUI points grow downwards; the world grows upwards. A half-pulled stick walks at half speed.
                Vector2 pull = (_stickNow - _stickOrigin) / StickRadius;
                if (pull.magnitude > DeadZone) move = new Vec2(pull.x, -pull.y).ClampLength(1);
            }

            var aim = Vec2.Zero;
            bool attack = keyboard != null && keyboard[Key.Space].isPressed;
            if (_attackFinger >= 0)
            {
                aim = Toward(camera, _attackPixels, player);
                attack = true;
            }
            else if (mouse != null)
            {
                aim = Toward(camera, mouse.position.ReadValue(), player);
                attack |= mouse.leftButton.isPressed;
            }
            // Keyboard-only and stick-only players face where they walk.
            if (aim.LengthSquared < 0.01f) aim = move;
            return (move, aim, attack);
        }

        public static bool LeavePressed() => Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame;

        public static bool BagPressed() =>
            Keyboard.current != null && (Keyboard.current[Key.Tab].wasPressedThisFrame || Keyboard.current[Key.I].wasPressedThisFrame);

        // The stick and the attack point over the game. Call from OnGUI.
        public static void DrawTouch()
        {
            if (_stickFinger >= 0)
            {
                Vector2 pull = Vector2.ClampMagnitude(_stickNow - _stickOrigin, StickRadius);
                Disc(Shapes.Ring, _stickOrigin, StickRadius, 0.3f);
                Disc(Shapes.Circle, _stickOrigin + pull, StickRadius * 0.45f, 0.45f);
            }
            if (_attackFinger >= 0) Disc(Shapes.Ring, Ui.FromScreen(_attackPixels), 28, 0.45f);
        }

        // A new finger: the stick on the left half, attack on the right, unless that role is taken or the UI owns the spot.
        static void Begin(int id, Vector2 pixels)
        {
            Vector2 point = Ui.FromScreen(pixels);
            bool left = point.x < Ui.Width / 2;
            if (Blocked(point))
            {
                _ignored.Add(id);
            }
            else if (left && _stickFinger < 0)
            {
                _stickFinger = id;
                _stickOrigin = _stickNow = point;
            }
            else if (!left && _attackFinger < 0)
            {
                _attackFinger = id;
                _attackPixels = pixels;
            }
            else
            {
                _ignored.Add(id);
            }
        }

        static Vec2 Toward(Camera camera, Vector2 screen, Vec2 player)
        {
            if (camera == null) return Vec2.Zero;
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            return new Vec2(world.x - player.X, world.y - player.Y);
        }

        static void Disc(Sprite sprite, Vector2 center, float radius, float alpha)
        {
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), sprite.texture);
            GUI.color = previous;
        }

        static bool Held(int id)
        {
            foreach (var touch in _touches)
            {
                if (touch.Id == id) return true;
            }
            return false;
        }

        static bool Blocked(Vector2 point)
        {
            foreach (var area in _blocked)
            {
                if (area.Contains(point)) return true;
            }
            return false;
        }
    }
}
