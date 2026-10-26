using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // Netcode for GameObjects used only as a pipe: one named message carries Core's byte arrays both ways. Nothing
    // is spawned as a NetworkObject; WorldHost and WorldClient do all the game work.
    sealed class NetLink : IDisposable
    {
        public const ushort DefaultPort = 7777;
        const string MessageName = "pray-to";

        readonly GameObject _object;
        readonly NetworkManager _manager;

        NetLink()
        {
            _object = new GameObject("Network");
            var transport = _object.AddComponent<UnityTransport>();
            _manager = _object.AddComponent<NetworkManager>();
            _manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            _manager.OnClientConnectedCallback += id => Connected?.Invoke(id);
            _manager.OnClientDisconnectCallback += id => Disconnected?.Invoke(id);
        }

        // Sender and payload.
        public event Action<ulong, byte[]> Received;
        // On the host, a client finished connecting; on a client, this client did.
        public event Action<ulong> Connected;
        public event Action<ulong> Disconnected;

        public ulong LocalClientId => _manager.LocalClientId;
        public string DisconnectReason => _manager.DisconnectReason;

        // Listens on every network interface so other machines on the LAN can join.
        public static NetLink Host(ushort port)
        {
            var link = new NetLink();
            link.Transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            return link.Start(host: true) ? link : link.Fail();
        }

        public static NetLink Join(string address, ushort port)
        {
            var link = new NetLink();
            link.Transport.SetConnectionData(address, port);
            return link.Start(host: false) ? link : link.Fail();
        }

        UnityTransport Transport => (UnityTransport)_manager.NetworkConfig.NetworkTransport;

        bool Start(bool host)
        {
            bool started = host ? _manager.StartHost() : _manager.StartClient();
            if (started) _manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            return started;
        }

        NetLink Fail()
        {
            Dispose();
            return null;
        }

        public void Send(ulong client, byte[] data, bool reliable)
        {
            if (!_manager.IsListening) return;
            using var writer = new FastBufferWriter(data.Length + sizeof(int), Allocator.Temp);
            writer.WriteValueSafe(data.Length);
            writer.WriteBytesSafe(data);
            var delivery = reliable ? NetworkDelivery.ReliableSequenced : NetworkDelivery.UnreliableSequenced;
            try
            {
                _manager.CustomMessagingManager.SendNamedMessage(MessageName, client, writer, delivery);
            }
            catch (Exception e)
            {
                // A client that just left can still be in the host's outgoing list for a tick.
                Debug.LogWarning($"Could not send to {client}: {e.Message}");
            }
        }

        void OnMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int length);
            if (length < 0 || length > reader.Length) return;
            var data = new byte[length];
            reader.ReadBytesSafe(ref data, length);
            Received?.Invoke(sender, data);
        }

        public void Dispose()
        {
            if (_manager != null && _manager.IsListening)
            {
                _manager.CustomMessagingManager?.UnregisterNamedMessageHandler(MessageName);
                _manager.Shutdown();
            }
            if (_object != null) Object.Destroy(_object);
        }
    }
}
