// Stand-ins for the Relay parts of Multiplayer Services 2.3 (com.unity.services.multiplayer,
// Runtime/Unity.Services.Multiplayer.asmdef). Signatures are copied from the package's Unity.Services.Multiplayer.api;
// nothing here runs.
using System;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Core;

namespace Unity.Services.Relay
{
    public interface IRelayService
    {
        Task<Models.Allocation> CreateAllocationAsync(int maxConnections, string region = default(string));
        Task<string> GetJoinCodeAsync(Guid allocationId);
        Task<Models.JoinAllocation> JoinAllocationAsync(string joinCode);
    }

    public static class RelayService
    {
        public static IRelayService Instance { get; }
    }

    // The reasons the game looks at; the package has many more.
    public enum RelayExceptionReason
    {
        InactiveProject = 15006,
        AllocationNotFound = 15008,
        JoinCodeNotFound = 15009,
        InvalidRequest = 15001,
        Unauthorized = 15401,
        Forbidden = 15403,
        NetworkError = 15998,
    }

    public class RelayServiceException : RequestFailedException
    {
        public RelayExceptionReason Reason { get; }
        public RelayServiceException(RelayExceptionReason reason, string message) : base((int)reason, message) => Reason = reason;
    }
}

namespace Unity.Services.Relay.Models
{
    public class Allocation
    {
        public Guid AllocationId { get; }
        public byte[] AllocationIdBytes { get; }
        public byte[] ConnectionData { get; }
        public byte[] Key { get; }
        public string Region { get; }
    }

    public class JoinAllocation
    {
        public Guid AllocationId { get; }
        public byte[] AllocationIdBytes { get; }
        public byte[] ConnectionData { get; }
        public byte[] HostConnectionData { get; }
        public byte[] Key { get; }
        public string Region { get; }
    }

    public static class AllocationUtils
    {
        public static RelayServerData ToRelayServerData(this Allocation allocation, string connectionType) => default;
        public static RelayServerData ToRelayServerData(this JoinAllocation allocation, string connectionType) => default;
    }
}
