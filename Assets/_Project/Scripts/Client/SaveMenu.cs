using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public SaveMenu(Action<string> pickCharacter, Action<string> pickWorld)
        {
            _pickCharacter = pickCharacter;
            _pickWorld = pickWorld;
        }

        // The panel, in a column this wide; character and world are the names the menu has now.
        public void Draw(float width, string character, string world)
        {
            if (Event.current.type == EventType.Layout)
            {
                PollPicker();
                if (Time.unscaledTime - _listedAt > ListSeconds) Relist();
            }
            var label = GUI.skin.label;
            GUILayout.Label("저장본 (이 기기)");
            GUILayout.Label(Ui.Wrap(
                SaveTransfer.InBrowser
                    ? "캐릭터와 월드를 파일로 내려받아 두거나 다른 기기로 옮긴다. 브라우저 데이터를 지우면 웹의 저장본도 사라지니 가끔 [전부 내보내기]로 남겨 둔다."
                    : "캐릭터와 월드를 파일로 내보내 두거나 다른 기기로 옮긴다.", label, width));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("전부 내보내기")) Act(() => Export(_keys, "전부 " + DateTime.Now.ToString("yyyy-MM-dd HHmm")));
            if (GUILayout.Button(_folderOpen ? "가져오기 닫기" : "가져오기")) Act(Import);
            GUILayout.EndHorizontal();
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
            if (GUILayout.Button("고르기"))
            {
                Act(() =>
                {
                    pick();
                    _confirmDelete = null;
                });
            }
            GUI.enabled = true;
            if (GUILayout.Button("내보내기")) Act(export);
            bool confirming = _confirmDelete == key;
            if (GUILayout.Button(confirming ? "정말 지우기" : "지우기"))
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

        void DrawClashes(float width)
        {
            GUILayout.Label(Ui.Wrap($"이미 있는 저장본을 바꾼다: {SaveTransfer.Describe(_clashes)}", GUI.skin.label, width));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("덮어쓰고 가져오기")) Act(Bring);
            if (GUILayout.Button("그만두기"))
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
            if (GUILayout.Button("폴더 열기")) SaveTransfer.OpenFolder();
            if (GUILayout.Button("목록 새로")) Act(() => _folderFiles = SaveTransfer.FilesInFolder());
            GUILayout.EndHorizontal();
            if (_folderFiles.Count == 0) GUILayout.Label(Ui.Wrap("폴더에 저장 파일이 없다.", GUI.skin.label, width));
            foreach (string path in _folderFiles)
            {
                if (GUILayout.Button(Ui.Wrap(Path.GetFileName(path), GUI.skin.button, width))) Act(() => ReadFile(path));
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

        void PollPicker()
        {
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
