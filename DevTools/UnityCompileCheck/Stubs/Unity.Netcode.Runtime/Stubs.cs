// Stand-ins for the parts of Netcode for GameObjects 2.13 (com.unity.netcode.gameobjects, Runtime/Unity.Netcode.Runtime.asmdef).
// Signatures are copied from the package source; nothing here runs.
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
