using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    // WASD or arrow keys to move, the mouse to aim, left button or space to attack (held to keep swinging).
    static class Controls
    {
        public static (Vec2 Move, Vec2 Aim, bool Attack) Read(Camera camera, Vec2 player)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

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

            var aim = Vec2.Zero;
            bool attack = keyboard != null && keyboard[Key.Space].isPressed;
            if (mouse != null && camera != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
                aim = new Vec2(world.x - player.X, world.y - player.Y);
                attack |= mouse.leftButton.isPressed;
            }
            // Keyboard-only players face where they walk.
            if (aim.LengthSquared < 0.01f) aim = move;
            return (move, aim, attack);
        }

        public static bool LeavePressed() => Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame;
    }
}
