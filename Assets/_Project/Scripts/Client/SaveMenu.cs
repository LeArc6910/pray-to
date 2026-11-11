using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The saves panel of the start menu, in the column the classes are in (GameRoot): the characters and worlds on
    // this device, each to pick, carry off in a file or delete; every save in one file; and bringing a file's saves
    // in, asking first when that would replace saves of the same name (SaveTransfer, Docs/Saving.md).
    sealed class SaveMenu
    {
        // The list is read again this often, so it keeps up with saves the menu itself does not make.
        const float ListSeconds = 1;

        readonly Action<string> _pickCharacter;
        readonly Action<string> _pickWorld;
        List<string> _characters = new List<string>();
        List<string> _worlds = new List<string>();
        List<string> _keys = new List<string>();
        float _listedAt = float.NegativeInfinity;
        string _message = "";
        // The save whose [지우기] asks once more.
        string _confirmDelete;
        // A file's saves waiting for [덮어쓰고 가져오기], and the ones already here that they would replace.
        List<(string Key, byte[] Data)> _incoming;
        List<string> _clashes;
        // Off the web: whether the files in the folder are shown, and which they are.
        bool _folderOpen;
        List<string> _folderFiles = new List<string>();
        // Cloud saves (CloudSaves, 2026-11-09): the account as typed, and an upload or a download under way.
        string _cloudName = "";
        string _cloudPassword = "";
        Task<string> _uploading;
        Task<(byte[] Bundle, string Message)> _downloading;

        public SaveMenu(Action<string> pickCharacter, Action<string> pickWorld)
        {
            _pickCharacter = pickCharacter;
            _pickWorld = pickWorld;
        }

        // The panel, in a column this wide; character and world are the names the menu has now.
        public void Draw(float width, string character, string world)
        {
            // Only this panel's own rows change here, before any of them is laid out.
            if (Event.current.type == EventType.Layout && Time.unscaledTime - _listedAt > ListSeconds) Relist();
            var label = GUI.skin.label;
            GUILayout.Label("저장본 (이 기기)");
            GUILayout.Label(Ui.Wrap(
                SaveTransfer.InBrowser
                    ? "캐릭터와 월드를 파일로 내려받아 두거나 다른 기기로 옮긴다. 브라우저 데이터를 지우면 웹의 저장본도 사라지니 가끔 [전부 내보내기]로 남겨 둔다."
                    : "캐릭터와 월드를 파일로 내보내 두거나 다른 기기로 옮긴다.", label, width));
            GUILayout.BeginHorizontal();
            if (Ui.LayoutButton("전부 내보내기")) Act(() => Export(_keys, "전부 " + DateTime.Now.ToString("yyyy-MM-dd HHmm")));
            if (Ui.LayoutButton(_folderOpen ? "가져오기 닫기" : "가져오기")) Act(Import);
            GUILayout.EndHorizontal();
            DrawCloud(width);
            if (_message.Length > 0) GUILayout.Label(Ui.Wrap(_message, label, width));
            if (_incoming != null) DrawClashes(width);
            if (_folderOpen) DrawFolder(width);

            GUILayout.Space(6);
            GUILayout.Label("캐릭터");
            if (_characters.Count == 0) GUILayout.Label(Ui.Wrap("아직 없다. 게임을 시작하면 저장된다.", label, width));
            foreach (string name in _characters)
            {
                DrawRow(width, SaveKeys.Character(name), name, name == character, () => _pickCharacter(name),
                    () => Export(SaveTransfer.CharacterSaves(name, _keys), "캐릭터 " + name),
                    () => SaveTransfer.CharacterSaves(name, _keys));
            }
            GUILayout.Space(6);
            GUILayout.Label("월드");
            if (_worlds.Count == 0) GUILayout.Label(Ui.Wrap("아직 없다. 혼자 하기나 호스트로 시작하면 저장된다.", label, width));
            foreach (string name in _worlds)
            {
                string key = SaveKeys.World(name);
                DrawRow(width, key, name, name == world, () => _pickWorld(name), () => Export(new[] { key }, "월드 " + name), () => new List<string> { key });
            }
        }

        // A save's name, "(고름)" when it is the one the menu has, and its buttons. Deleting asks once more and takes
        // what goes with it (a character's maps).
        void DrawRow(float width, string key, string name, bool picked, Action pick, Action export, Func<List<string>> belonging)
        {
            GUILayout.Label(Ui.Wrap(picked ? $"{name} (고름)" : name, GUI.skin.label, width));
            GUILayout.BeginHorizontal();
            GUI.enabled = !picked;
            if (Ui.LayoutButton("고르기"))
            {
                Act(() =>
                {
                    pick();
                    _confirmDelete = null;
                });
            }
            GUI.enabled = true;
            if (Ui.LayoutButton("내보내기")) Act(export);
            bool confirming = _confirmDelete == key;
            if (Ui.LayoutButton(confirming ? "정말 지우기" : "지우기"))
            {
                Act(() =>
                {
                    if (!confirming)
                    {
                        _confirmDelete = key;
                        return;
                    }
                    var keys = belonging();
                    foreach (string gone in keys) SaveStore.Delete(gone);
                    _message = $"지웠습니다: {SaveTransfer.Describe(keys)}";
                    _confirmDelete = null;
                    Relist();
                });
            }
            GUILayout.EndHorizontal();
        }

        // A name and a password, and [올리기], [받기] and [계정 만들기]. What comes down goes in as a file's saves do,
        // asking before it replaces any.
        void DrawCloud(float width)
        {
            GUILayout.Space(6);
            GUILayout.Label("클라우드 저장");
            GUILayout.Label(Ui.Wrap("아이디와 비밀번호로 이 기기의 저장본을 클라우드에 올려 두고 다른 기기(웹·PC·휴대폰)에서 받는다. 받을 때 이미 있는 저장본을 바꾸게 되면 먼저 묻는다.", GUI.skin.label, width));
            bool busy = _uploading != null || _downloading != null;
            GUI.enabled = !busy;
            GUILayout.BeginHorizontal();
            GUILayout.Label("아이디", GUILayout.Width(76));
            _cloudName = GUILayout.TextField(_cloudName, CloudAccount.NameMost);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("비밀번호", GUILayout.Width(76));
            _cloudPassword = GUILayout.PasswordField(_cloudPassword, '*', CloudAccount.PasswordMost);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Ui.LayoutButton("올리기")) Act(() => StartUpload(false));
            if (Ui.LayoutButton("받기")) Act(StartDownload);
            if (Ui.LayoutButton("계정 만들기")) Act(() => StartUpload(true));
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (busy) GUILayout.Label("클라우드와 주고받는 중…");
        }

        void StartUpload(bool create)
        {
            _message = CloudAccount.Check(_cloudName, _cloudPassword) ?? "";
            if (_message.Length == 0) _uploading = CloudSaves.Upload(_cloudName.Trim(), _cloudPassword, create);
        }

        void StartDownload()
        {
            _incoming = null;
            _clashes = null;
            _message = CloudAccount.Check(_cloudName, _cloudPassword) ?? "";
            if (_message.Length == 0) _downloading = CloudSaves.Download(_cloudName.Trim(), _cloudPassword);
        }

        // An upload or a download that has finished, from Update like a picked file.
        void PollCloud()
        {
            if (_uploading != null && _uploading.IsCompleted)
            {
                _message = _uploading.Status == TaskStatus.RanToCompletion ? _uploading.Result : "올리지 못했습니다.";
                _uploading = null;
                Relist();
            }
            if (_downloading != null && _downloading.IsCompleted)
            {
                var (bundle, message) = _downloading.Status == TaskStatus.RanToCompletion ? _downloading.Result : (null, "받지 못했습니다.");
                _downloading = null;
                if (bundle != null) Receive(bundle);
                else _message = message;
            }
        }

        void DrawClashes(float width)
        {
            GUILayout.Label(Ui.Wrap($"이미 있는 저장본을 바꾼다: {SaveTransfer.Describe(_clashes)}", GUI.skin.label, width));
            GUILayout.BeginHorizontal();
            if (Ui.LayoutButton("덮어쓰고 가져오기")) Act(Bring);
            if (Ui.LayoutButton("그만두기"))
            {
                Act(() =>
                {
                    _incoming = null;
                    _clashes = null;
                    _message = "가져오지 않았습니다.";
                });
            }
            GUILayout.EndHorizontal();
        }

        // Off the web: the folder, and a button for each save file in it.
        void DrawFolder(float width)
        {
            GUILayout.Label(Ui.Wrap($"이 폴더에 넣은 {SaveTransfer.Extension} 파일을 가져온다: {SaveTransfer.Folder}", GUI.skin.label, width));
            GUILayout.BeginHorizontal();
            if (Ui.LayoutButton("폴더 열기")) SaveTransfer.OpenFolder();
            if (Ui.LayoutButton("목록 새로")) Act(() => _folderFiles = SaveTransfer.FilesInFolder());
            GUILayout.EndHorizontal();
            if (_folderFiles.Count == 0) GUILayout.Label(Ui.Wrap("폴더에 저장 파일이 없다.", GUI.skin.label, width));
            foreach (string path in _folderFiles)
            {
                if (Ui.LayoutButton(Ui.Wrap(Path.GetFileName(path), GUI.skin.button, width))) Act(() => ReadFile(path));
            }
        }

        // Does what a button asked, then ends this pass of the GUI: what the panel shows may have changed (a message,
        // a row more or less), and IMGUI must lay it out again before drawing it.
        static void Act(Action action)
        {
            action();
            GUIUtility.ExitGUI();
        }

        void Relist()
        {
            _keys = SaveStore.List();
            _characters = Names(SaveKind.Character);
            _worlds = Names(SaveKind.World);
            _listedAt = Time.unscaledTime;
        }

        List<string> Names(SaveKind wanted) =>
            _keys.Select(key => SaveKeys.TryParse(key, out var kind, out string name) && kind == wanted ? name : null)
                .Where(name => name != null)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

        const string ExportedKey = "saves.exported";
        const int ExportDays = 7;

        // Saves are kept, it is a web or touch device, and none were exported in the last ExportDays (or ever).
        public static bool ExportDue
        {
            get
            {
                if (!(Application.platform == RuntimePlatform.WebGLPlayer || Controls.UsingTouch) || SaveStore.List().Count == 0) return false;
                string when = Prefs.Get(ExportedKey, "");
                return !DateTime.TryParse(when, null, System.Globalization.DateTimeStyles.RoundtripKind, out var at) || (DateTime.UtcNow - at).TotalDays >= ExportDays;
            }
        }

        public static string ExportReminder =>
            Prefs.Get(ExportedKey, "").Length == 0
                ? Strings.T("menu.export_first", "저장본은 이 브라우저(앱)에만 있습니다. 브라우저 데이터를 지우면 사라지니 [저장본]에서 [전부 내보내기]로 파일을 남겨 두세요.")
                : string.Format(Strings.T("menu.export_again", "마지막으로 내보낸 지 {0}일이 넘었습니다. [저장본]에서 [전부 내보내기]로 파일을 새로 남겨 두세요."), ExportDays);

        void Export(IEnumerable<string> keys, string what)
        {
            var list = keys.ToList();
            if (list.Count == 0)
            {
                _message = "내보낼 저장본이 없습니다.";
                return;
            }
            try
            {
                _message = SaveTransfer.Deliver(SaveTransfer.FileName(what), SaveTransfer.Pack(list));
                Prefs.Set(ExportedKey, DateTime.UtcNow.ToString("o"));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _message = $"내보내지 못했습니다: {e.Message}";
            }
        }

        void Import()
        {
            _incoming = null;
            _clashes = null;
            if (SaveTransfer.InBrowser)
            {
                SaveTransfer.StartPick($"가져올 pray-to 저장 파일({SaveTransfer.Extension})을 고르세요.", "파일 고르기", "그만두기");
                _message = "파일을 고르는 중…";
                return;
            }
            _folderOpen = !_folderOpen;
            if (_folderOpen) _folderFiles = SaveTransfer.FilesInFolder();
            _message = "";
        }

        // In a browser: takes the picked file once it is there. Called from Update, before the menu is laid out, since
        // bringing saves in changes the menu's other column too (a world now saved has [월드 지우기]), and IMGUI must
        // draw what it laid out.
        public void Poll()
        {
            PollCloud();
            switch (SaveTransfer.PollPick(out byte[] file))
            {
                case SaveTransfer.Pick.Ready:
                    Receive(file);
                    break;
                case SaveTransfer.Pick.Cancelled:
                    _message = "가져오지 않았습니다.";
                    break;
                case SaveTransfer.Pick.Failed:
                    _message = "그 파일을 읽지 못했습니다(너무 크거나 열 수 없다).";
                    break;
            }
        }

        void ReadFile(string path)
        {
            try
            {
                Receive(File.ReadAllBytes(path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _message = $"그 파일을 읽지 못했습니다: {e.Message}";
            }
        }

        // A file's saves come in at once, or after [덮어쓰고 가져오기] when some are already here.
        void Receive(byte[] file)
        {
            try
            {
                _incoming = SaveBundle.Read(file);
            }
            catch (InvalidDataException e)
            {
                _message = $"가져오지 못했습니다: {e.Message}";
                return;
            }
            if (_incoming.Count == 0)
            {
                _incoming = null;
                _message = "저장 파일이 비어 있습니다.";
                return;
            }
            _clashes = _incoming.Select(save => save.Key).Where(SaveStore.Exists).ToList();
            if (_clashes.Count == 0) Bring();
            else _message = "";
        }

        // Writes the saves and picks the character and the world they hold, when there is one of each.
        void Bring()
        {
            var keys = _incoming.Select(save => save.Key).ToList();
            try
            {
                SaveTransfer.Bring(_incoming);
                _message = $"가져왔습니다: {SaveTransfer.Describe(keys)}";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _message = $"가져오지 못했습니다: {e.Message}";
            }
            _incoming = null;
            _clashes = null;
            _folderOpen = false;
            Relist();
            var characters = keys.Where(key => SaveKeys.TryParse(key, out var kind, out _) && kind == SaveKind.Character).ToList();
            var worlds = keys.Where(key => SaveKeys.TryParse(key, out var kind, out _) && kind == SaveKind.World).ToList();
            if (characters.Count == 1 && SaveKeys.TryParse(characters[0], out _, out string character)) _pickCharacter(character);
            if (worlds.Count == 1 && SaveKeys.TryParse(worlds[0], out _, out string world)) _pickWorld(world);
        }
    }
}
