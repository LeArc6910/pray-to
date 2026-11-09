using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

namespace Game.Client
{
    // Saves kept in Unity Cloud Save (Docs/Saving.md, 클라우드 저장, 2026-11-09): every save of a device in one bundle
    // (SaveBundle, as [전부 내보내기] packs it) under a player account of a name and a password, so the web, a PC and
    // a phone can share them. Uploading lays this device's saves over what the cloud already has, so a save only
    // another device uploaded stays. Each upload or download signs in for itself and signs out after, so online play
    // goes on as a fresh anonymous player each run (NetLink).
    static class CloudSaves
    {
        // The one cloud file of an account.
        const string FileKey = "saves";

        // Uploads every save on this device. create first makes the account. Returns what happened, for the screen.
        public static async Task<string> Upload(string name, string password, bool create)
        {
            var device = SaveStore.List().Select(key => (key, SaveStore.Read(key))).Where(save => save.Item2 != null && save.Item2.Length > 0).ToList();
            if (device.Count == 0) return "올릴 저장본이 없습니다.";
            string failed = await SignIn(name, password, create);
            if (failed != null) return failed;
            try
            {
                var cloud = await LoadOrNull();
                var kept = cloud == null ? new List<(string Key, byte[] Data)>() : SaveBundle.Read(cloud);
                var merged = SaveBundle.Over(kept, device);
                await CloudSaveService.Instance.Files.Player.SaveAsync(FileKey, SaveBundle.Write(merged));
                int others = merged.Count - device.Count;
                return $"클라우드에 올렸습니다: {SaveTransfer.Describe(device.Select(s => s.key))}"
                    + (others > 0 ? $". 다른 기기에서 올린 {others}개는 그대로 둡니다." : "");
            }
            catch (System.IO.InvalidDataException e)
            {
                return $"클라우드의 저장본을 읽지 못해 올리지 않았습니다: {e.Message}";
            }
            catch (Exception e)
            {
                return Explain(e, "올리지 못했습니다");
            }
            finally
            {
                SignOut();
            }
        }

        // The account's bundle, or null with what went wrong.
        public static async Task<(byte[] Bundle, string Message)> Download(string name, string password)
        {
            string failed = await SignIn(name, password, false);
            if (failed != null) return (null, failed);
            try
            {
                var bundle = await LoadOrNull();
                return bundle == null ? (null, "이 계정의 클라우드에는 아직 올린 저장본이 없습니다.") : (bundle, null);
            }
            catch (Exception e)
            {
                return (null, Explain(e, "받지 못했습니다"));
            }
            finally
            {
                SignOut();
            }
        }

        static async Task<byte[]> LoadOrNull()
        {
            try
            {
                return await CloudSaveService.Instance.Files.Player.LoadBytesAsync(FileKey);
            }
            catch (CloudSaveException e) when (e.Reason == CloudSaveExceptionReason.NotFound)
            {
                return null;
            }
        }

        // Null when signed in to the account, else why not.
        static async Task<string> SignIn(string name, string password, bool create)
        {
            string wrong = CloudAccount.Check(name, password);
            if (wrong != null) return wrong;
            try
            {
                await NetLink.StartServices();
                SignOut();
                if (create) await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(name, password);
                else await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(name, password);
                return null;
            }
            catch (Exception e)
            {
                return Explain(e, create ? "계정을 만들지 못했습니다(이미 있는 아이디일 수 있다)" : "로그인하지 못했습니다(아이디·비밀번호를 확인하거나, 처음이면 [계정 만들기])");
            }
        }

        static void SignOut()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
                    AuthenticationService.Instance.SignOut(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Cloud saves: signing out: {e}");
            }
        }

        static string Explain(Exception e, string what)
        {
            Debug.LogWarning($"Cloud saves: {e}");
            if (e is ServicesInitializationException) return $"{what}: 온라인 기능이 아직 설정되지 않았습니다(Unity 프로젝트 연결). ({e.Message})";
            if (e is CloudSaveException cloud)
            {
                switch (cloud.Reason)
                {
                    case CloudSaveExceptionReason.NoInternetConnection:
                        return $"{what}: 인터넷에 연결할 수 없습니다.";
                    case CloudSaveExceptionReason.Unauthorized:
                    case CloudSaveExceptionReason.ProjectIdMissing:
                        return $"{what}: 클라우드 저장이 아직 켜지지 않았습니다(Unity 대시보드의 Cloud Save). ({e.Message})";
                    case CloudSaveExceptionReason.TooManyRequests:
                        return $"{what}: 너무 자주 눌렀습니다. 잠시 뒤에 다시 하세요.";
                }
            }
            return $"{what}. ({e.Message})";
        }
    }
}
