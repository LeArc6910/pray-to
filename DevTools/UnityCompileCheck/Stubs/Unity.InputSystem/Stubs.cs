// Stand-ins for the parts of Input System 1.20 (com.unity.inputsystem, InputSystem/Unity.InputSystem.asmdef).
// Signatures are copied from the package source; nothing here runs.
using UnityEngine;

namespace UnityEngine.InputSystem.Controls
{
    public class InputControl<TValue> where TValue : struct
    {
        public TValue ReadValue() => default;
    }

    public class ButtonControl : InputControl<float>
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
    }

    public class KeyControl : ButtonControl
    {
    }

    public class Vector2Control : InputControl<Vector2>
    {
    }
}

namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;

    public enum Key
    {
        None,
        Space,
        Enter,
        Tab,
        I,
        A,
        D,
        S,
        W,
        Escape,
        LeftArrow,
        RightArrow,
        UpArrow,
        DownArrow,
    }

    public class Keyboard
    {
        public static Keyboard current { get; private set; }
        public KeyControl this[Key key] => null;
    }

    public class Mouse
    {
        public static Mouse current { get; private set; }
        public ButtonControl leftButton { get; protected set; }
        public Vector2Control position { get; protected set; }
    }
}
