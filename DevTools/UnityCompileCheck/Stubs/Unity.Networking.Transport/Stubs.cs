// Stand-ins for the parts of Unity Transport (com.unity.transport, Runtime/Unity.Networking.Transport.asmdef) the game
// passes along. Signatures are copied from the package source; nothing here runs.
namespace Unity.Networking.Transport.Relay
{
    // What a Relay allocation gives Unity Transport to connect through. The game only hands it on, from
    // AllocationUtils.ToRelayServerData to UnityTransport.SetRelayServerData.
    public struct RelayServerData
    {
        public bool IsSecure;
        public bool IsWebSocket;
    }
}
