// Stand-ins for the parts of Unity Gaming Services Core 1.18 (com.unity.services.core, Runtime/Core/Unity.Services.Core.asmdef).
// Signatures are copied from the package's Unity.Services.Core.api; nothing here runs.
using System;
using System.Threading.Tasks;

namespace Unity.Services.Core
{
    public enum ServicesInitializationState
    {
        Uninitialized = 0,
        Initializing = 1,
        Initialized = 2,
    }

    public class InitializationOptions
    {
        public InitializationOptions() { }
        public InitializationOptions SetOption(string key, string value) => this;
    }

    public class RequestFailedException : Exception
    {
        public int ErrorCode { get; }
        public RequestFailedException(int errorCode, string message) : base(message) => ErrorCode = errorCode;
        public RequestFailedException(int errorCode, string message, Exception innerException) : base(message, innerException) => ErrorCode = errorCode;
    }

    public class ServicesInitializationException : Exception
    {
        public ServicesInitializationException() { }
        public ServicesInitializationException(string message) : base(message) { }
        public ServicesInitializationException(string message, Exception inner) : base(message, inner) { }
    }

    public static class UnityServices
    {
        public static ServicesInitializationState State { get; }
        public static Task InitializeAsync() => Task.CompletedTask;
        public static Task InitializeAsync(InitializationOptions options) => Task.CompletedTask;
    }
}
