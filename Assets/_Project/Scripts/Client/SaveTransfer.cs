using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Saves carried off this device and back in one file (SaveBundle; Docs/Saving.md 내보내기와 가져오기). In a
    // browser the file is downloaded, and brought back with the browser's own file picker (Plugins/WebGL/Files.jslib).
    // Elsewhere it is written to a folder (Folder, in Documents), and the files put in that folder can be brought in.
    static class SaveTransfer
    {
        public const string Extension = ".ptsave";
        // Far bigger than a bundle of real saves; a picked file bigger than this is refused unread.
        const int MaxFileBytes = 64 * 1024 * 1024;

        // The saves that go with a character: it, and its map of every world it has been in.
        public static List<string> CharacterSaves(string name, IEnumerable<string> keys) =>
            keys.Where(key => SaveKeys.TryParse(key, out var kind, out string owner) && kind != SaveKind.World && owner == name).ToList();

        // These saves in one file; a key with no save is left out.
        public static byte[] Pack(IEnumerable<string> keys) =>
            SaveBundle.Write(keys.Select(key => (key, SaveStore.Read(key))).Where(save => save.Item2 != null).ToList());

        // Every save in the bundle onto this device, in place of any of the same name.
        public static void Bring(IEnumerable<(string Key, byte[] Data)> saves)
        {
            foreach (var (key, data) in saves) SaveStore.Write(key, data);
        }

        // What these saves are, for a message: "캐릭터 '모험가', 월드 '내 월드', 지도 2개".
        public static string Describe(IEnumerable<string> keys)
        {
            var parts = new List<string>();
            int maps = 0;
            foreach (string key in keys)
            {
                if (SaveKeys.TryParse(key, out var kind, out _) && kind == SaveKind.Map) maps++;
                else parts.Add(SaveBundle.Describe(key));
            }
            if (maps > 0) parts.Add($"지도 {maps}개");
            return string.Join(", ", parts);
        }

        // "pray-to 캐릭터 모험가.ptsave", with whatever a file name cannot hold made "_".
        public static string FileName(string what)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
            string name = new string(("pray-to " + what).Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray());
            return name.Trim().TrimEnd('.') + Extension;
        }

        // Off the web: where files go and where files to bring in are looked for. Tests point it elsewhere.
        static string _folder;

        public static string Folder
        {
            get => _folder ??= DefaultFolder();
            internal set => _folder = value;
        }

        static string DefaultFolder()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrEmpty(documents) ? DeviceFiles.PathOf("exports") : Path.Combine(documents, "pray-to");
        }

        public static bool InBrowser
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        // Gives the player the file: a download in a browser, a file in Folder elsewhere. Returns what to tell them.
        public static string Deliver(string fileName, byte[] data)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SaveFileToDevice(fileName, data, data.Length);
            return $"'{fileName}' 파일을 내려받았습니다.";
#else
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, fileName);
            File.WriteAllBytes(path, data);
            return $"{path}에 저장했습니다.";
#endif
        }

        // Off the web: the save files in Folder, newest first.
        public static List<string> FilesInFolder()
        {
            if (!Directory.Exists(Folder)) return new List<string>();
            return Directory.GetFiles(Folder, "*" + Extension)
                .Where(path => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();
        }

        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            string path = Folder.Replace('\\', '/');
            Application.OpenURL("file://" + (path.StartsWith("/") ? "" : "/") + path);
        }

        public enum Pick
        {
            None,
            Waiting,
            Ready,
            Cancelled,
            Failed,
        }

        // In a browser: shows the box with the button that opens the file picker (Files.jslib).
        public static void StartPick(string text, string choose, string cancel)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PickFileStart(text, choose, cancel, MaxFileBytes);
#endif
        }

        // How picking goes; the file once it is Ready (then it is None again).
        public static Pick PollPick(out byte[] file)
        {
            file = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            var state = (Pick)PickFileState();
            if (state != Pick.Ready) return state;
            file = new byte[PickFileLength()];
            PickFileTake(file, file.Length);
            return Pick.Ready;
#else
            return Pick.None;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void SaveFileToDevice(string name, byte[] data, int length);

        [DllImport("__Internal")]
        static extern void PickFileStart(string text, string choose, string cancel, int maxBytes);

        [DllImport("__Internal")]
        static extern int PickFileState();

        [DllImport("__Internal")]
        static extern int PickFileLength();

        [DllImport("__Internal")]
        static extern void PickFileTake(byte[] buffer, int length);
#endif
    }
}
