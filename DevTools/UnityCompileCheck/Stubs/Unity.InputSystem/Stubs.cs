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

    public class IntegerControl : InputControl<int>
    {
    }

    public class TouchPressControl : ButtonControl
    {
    }

    // The real class is an InputControl<TouchState>; only the child controls are used.
    public class TouchControl
    {
        public TouchPressControl press { get; set; }
        public IntegerControl touchId { get; set; }
        public Vector2Control position { get; set; }
    }
}

namespace UnityEngine.InputSystem.Utilities
{
    using System.Collections;
    using System.Collections.Generic;

    public struct ReadOnlyArray<TValue> : IReadOnlyList<TValue>
    {
        public int Count => 0;
        public TValue this[int index] => default;
        public IEnumerator<TValue> GetEnumerator() => null;
        IEnumerator IEnumerable.GetEnumerator() => null;
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
        M,
        Q,
        E,
        R,
        F,
        C,
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

    public class Touchscreen
    {
        public static Touchscreen current { get; internal set; }
        public UnityEngine.InputSystem.Utilities.ReadOnlyArray<TouchControl> touches { get; protected set; }
    }

    public class Mouse
    {
        public static Mouse current { get; private set; }
        public ButtonControl leftButton { get; protected set; }
        public Vector2Control position { get; protected set; }
    }
}
