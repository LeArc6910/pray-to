using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
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
        const int FragmentAbove = 1000;

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

        // Where a host listens: every network interface, so other machines on the LAN can join. The play mode tests
        // listen on this machine only (127.0.0.1), which needs no firewall permission, so they run without anyone
        // answering Windows' firewall prompt.
        internal static string ListenAddress { get; set; } = "0.0.0.0";

        public static NetLink Host(ushort port)
        {
            var link = new NetLink();
            link.Transport.SetConnectionData("127.0.0.1", port, ListenAddress);
            return link.Start(host: true) ? link : link.Fail();
        }

        public static NetLink Join(string address, ushort port)
        {
            var link = new NetLink();
            link.Transport.SetConnectionData(address, port);
            return link.Start(host: false) ? link : link.Fail();
        }

        // ---- Online, through Unity Relay ----
        //
        // Over the internet nobody listens on a port of their own: the host and the players who join all connect out to
        // a Unity Relay server, which passes their messages on. The host gets a short room code to give the others.
        // Everyone connects by secure WebSocket ("wss"), the one way a browser can, so the web demo and the PC version
        // play together. It needs the project linked to Unity Gaming Services with Relay switched on
        // (Docs/Multiplayer.md, 온라인).

        // Makes a room for this many others and returns the started host link and its code, or an error to show.
        public static async Task<(NetLink Link, string Code, string Error)> HostOnline(int guests)
        {
            string error = await SignIn();
            if (error != null) return (null, null, error);
            NetLink link = null;
            try
            {
                var allocation = await RelayService.Instance.CreateAllocationAsync(guests);
                string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                link = new NetLink();
                link.Transport.UseWebSockets = true;
                link.Transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnection));
                return link.Start(host: true) ? (link, code, null) : (link.Fail(), null, "호스트를 시작하지 못했습니다.");
            }
            catch (Exception e)
            {
                link?.Dispose();
                return (null, null, Explain(e));
            }
        }

        // Looks up the room and returns a link ready to connect to it, not yet started, so the caller can listen for
        // its events first (StartClient); or an error to show.
        public static async Task<(NetLink Link, string Error)> JoinOnline(string code)
        {
            string error = await SignIn();
            if (error != null) return (null, error);
            try
            {
                var allocation = await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
                var link = new NetLink();
                link.Transport.UseWebSockets = true;
                link.Transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnection));
                return (link, null);
            }
            catch (Exception e)
            {
                return (null, Explain(e));
            }
        }

        // Starts a link from JoinOnline. False when it could not; the link is then closed.
        public bool StartClient()
        {
            if (Start(host: false)) return true;
            Dispose();
            return false;
        }

        const string RelayConnection = "wss";

        // Signs in to Unity Gaming Services once a run, as a new anonymous player each time: two tabs of the same
        // browser (or two copies on one PC) are then two players. Null when it worked, else what went wrong.
        static async Task<string> SignIn()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    var options = new InitializationOptions().SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 12));
                    await UnityServices.InitializeAsync(options);
                }
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return null;
            }
            catch (Exception e)
            {
                return Explain(e);
            }
        }

        // A reason a player can act on, with the service's own message for the rest.
        static string Explain(Exception e)
        {
            Debug.LogWarning($"Online play: {e}");
            if (e is RelayServiceException relay)
            {
                switch (relay.Reason)
                {
                    case RelayExceptionReason.JoinCodeNotFound:
                    case RelayExceptionReason.AllocationNotFound:
                    case RelayExceptionReason.InvalidRequest:
                        return "그 코드의 방이 없습니다. 코드를 확인하세요.";
                    case RelayExceptionReason.InactiveProject:
                    case RelayExceptionReason.Unauthorized:
                    case RelayExceptionReason.Forbidden:
                        return "온라인 기능이 아직 켜지지 않았습니다(Unity 대시보드의 Relay). (" + e.Message + ")";
                    case RelayExceptionReason.NetworkError:
                        return "인터넷에 연결할 수 없습니다. (" + e.Message + ")";
                }
                return "방을 열거나 찾지 못했습니다. (" + e.Message + ")";
            }
            if (e is AuthenticationException) return "Unity 서비스에 로그인하지 못했습니다. (" + e.Message + ")";
            if (e is ServicesInitializationException) return "온라인 기능이 아직 설정되지 않았습니다(Unity 프로젝트 연결). (" + e.Message + ")";
            return e.Message;
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
            // A full bag can be bigger than one packet; only reliable messages may be split up.
            var delivery = !reliable ? NetworkDelivery.UnreliableSequenced
                : data.Length > FragmentAbove ? NetworkDelivery.ReliableFragmentedSequenced
                : NetworkDelivery.ReliableSequenced;
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
