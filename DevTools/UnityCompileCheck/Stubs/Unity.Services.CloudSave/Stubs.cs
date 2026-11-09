// Stand-ins for the parts of Unity Cloud Save 3.4 (com.unity.services.cloudsave, Runtime/Unity.Services.CloudSave.asmdef).
// Signatures are copied from the package's Unity.Services.CloudSave.api; nothing here runs.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;

namespace Unity.Services.CloudSave
{
    public class CloudSaveException : RequestFailedException
    {
        public CloudSaveExceptionReason Reason { get; }
        CloudSaveException(int errorCode, string message) : base(errorCode, message) { }
    }

    public enum CloudSaveExceptionReason
    {
        Unknown = 0,
        NoInternetConnection = 1,
        ProjectIdMissing = 2,
        PlayerIdMissing = 3,
        AccessTokenMissing = 4,
        InvalidArgument = 5,
        Unauthorized = 6,
        KeyLimitExceeded = 7,
        NotFound = 8,
        TooManyRequests = 9,
        ServiceUnavailable = 10,
        Conflict = 11,
    }

    public static class CloudSaveService
    {
        public static ICloudSaveService Instance { get; }
    }

    public interface ICloudSaveService
    {
        public Internal.IFilesService Files { get; }
    }

    public class SaveOptions
    {
        public Nullable<int> RequestTimeout { get; set; }
        public SaveOptions() { }
    }

    public class DeleteOptions
    {
        public DeleteOptions() { }
    }
}

namespace Unity.Services.CloudSave.Internal
{
    public interface IFilesService
    {
        public IPlayerFilesService Player { get; }
    }

    public interface IPlayerFilesService
    {
        public Task DeleteAsync(string key, DeleteOptions options = default(DeleteOptions));
        public Task<Unity.Services.CloudSave.Models.FileItem> GetMetadataAsync(string key);
        public Task<List<Unity.Services.CloudSave.Models.FileItem>> ListAllAsync();
        public Task<byte[]> LoadBytesAsync(string key);
        public Task SaveAsync(string key, byte[] bytes, SaveOptions options = default(SaveOptions));
    }
}

namespace Unity.Services.CloudSave.Models
{
    public class FileItem
    {
        public string ContentType { get; }
        public Nullable<DateTime> Created { get; }
        public string Key { get; }
        public Nullable<DateTime> Modified { get; }
        public long Size { get; }
        public string WriteLock { get; }
    }
}
