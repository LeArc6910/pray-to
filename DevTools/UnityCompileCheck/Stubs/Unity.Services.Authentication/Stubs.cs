// Stand-ins for the parts of Unity Authentication 3.7 (com.unity.services.authentication,
// Runtime/Unity.Services.Authentication.asmdef). Signatures are copied from the package's
// Unity.Services.Authentication.api; nothing here runs.
using System.Threading.Tasks;
using Unity.Services.Core;

namespace Unity.Services.Authentication
{
    public static class AuthenticationService
    {
        public static IAuthenticationService Instance { get; }
    }

    public interface IAuthenticationService
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }
        Task SignInAnonymouslyAsync(SignInOptions options = default(SignInOptions));
        Task SignInWithUsernamePasswordAsync(string username, string password);
        Task SignUpWithUsernamePasswordAsync(string username, string password);
        void SignOut(bool clearCredentials = false);
    }

    public sealed class SignInOptions
    {
        public bool CreateAccount { get; set; }
    }

    public sealed class AuthenticationException : RequestFailedException
    {
        AuthenticationException(int errorCode, string message) : base(errorCode, message) { }
    }

    public static class AuthenticationExtensions
    {
        public static InitializationOptions SetProfile(this InitializationOptions options, string profile) => options;
    }
}
