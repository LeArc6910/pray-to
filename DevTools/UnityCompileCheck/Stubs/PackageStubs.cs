// Stand-ins for the parts of Unity packages that the Client scripts use: Netcode for GameObjects 2.13, Input System
// 1.20, URP 17.6 and the Test Framework. Signatures are copied from the packages' sources; nothing here runs. When a script starts using
// more of a package, add the member here with the package's exact signature.
using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.Netcode
{
    public enum NetworkDelivery
    {
        Unreliable,
        UnreliableSequenced,
        Reliable,
        ReliableSequenced,
        ReliableFragmentedSequenced,
    }

    public abstract class NetworkTransport : MonoBehaviour
    {
    }

    [Serializable]
    public class NetworkConfig
    {
        public NetworkTransport NetworkTransport = null;
        public bool ConnectionApproval = false;
        public uint TickRate = 30;
    }

    public class NetworkManager : MonoBehaviour
    {
        public const ulong ServerClientId = 0;
        public NetworkConfig NetworkConfig;
        public static NetworkManager Singleton { get; private set; }
        public ulong LocalClientId { get; internal set; }
        public bool IsServer => false;
        public bool IsClient => false;
        public bool IsHost => false;
        public bool IsListening => false;
        public string DisconnectReason => "";
        public CustomMessagingManager CustomMessagingManager { get; private set; }
        public event Action<ulong> OnClientConnectedCallback { add { } remove { } }
        public event Action<ulong> OnClientDisconnectCallback { add { } remove { } }
        public bool StartHost() => false;
        public bool StartClient() => false;
        public bool StartServer() => false;
        public void Shutdown(bool discardMessageQueue = false) { }
    }

    public class CustomMessagingManager
    {
        public delegate void HandleNamedMessageDelegate(ulong senderClientId, FastBufferReader messagePayload);

        public void RegisterNamedMessageHandler(string name, HandleNamedMessageDelegate callback) { }
        public void UnregisterNamedMessageHandler(string name) { }
        public void SendNamedMessage(string messageName, ulong clientId, FastBufferWriter messageStream, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced) { }
    }

    public struct FastBufferWriter : IDisposable
    {
        public struct ForPrimitives
        {
        }

        public FastBufferWriter(int size, Allocator allocator, int maxSize = -1) { }

        public int Length => 0;

        public void WriteValueSafe<T>(in T value, ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> { }
        public void WriteBytesSafe(byte[] value, int size = -1, int offset = 0) { }
        public void Dispose() { }
    }

    public struct FastBufferReader : IDisposable
    {
        public int Length => 0;

        public void ReadValueSafe<T>(out T value, FastBufferWriter.ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> => value = default;
        public void ReadBytesSafe(ref byte[] value, int size, int offset = 0) { }
        public void Dispose() { }
    }
}

namespace Unity.Netcode.Transports.UTP
{
    public class UnityTransport : Unity.Netcode.NetworkTransport
    {
        public void SetConnectionData(string ipv4Address, ushort port, string listenAddress = null) { }
    }
}

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

namespace UnityEngine.Rendering.Universal
{
    public abstract class ScriptableRendererData : ScriptableObject
    {
    }

    public class Renderer2DData : ScriptableRendererData
    {
    }

    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;

        protected override RenderPipeline CreatePipeline() => null;
    }

    public sealed class Light2D : MonoBehaviour
    {
        public enum LightType
        {
            Parametric = 0,
            Freeform = 1,
            Sprite = 2,
            Point = 3,
            Global = 4,
        }

        public LightType lightType { get; set; }
        public float intensity { get; set; }
        public Color color { get; set; }
    }
}

namespace UnityEngine.TestTools
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UnityTestAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class UnityTearDownAttribute : Attribute
    {
    }
}
