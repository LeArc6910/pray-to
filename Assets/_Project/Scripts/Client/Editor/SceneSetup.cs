using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Client.Editor
{
    // Builds everything the game needs in the editor from code, so nobody has to wire it by hand:
    // the GameSheets asset pointing at the CSV sheets, and the Main scene with a camera and GameRoot.
    public static class SceneSetup
    {
        public const string ProjectFolder = "Assets/_Project";
        const string ScenePath = ProjectFolder + "/Scenes/Main.unity";
        const string SheetsPath = ProjectFolder + "/Resources/" + GameSheets.ResourcePath + ".asset";
        const string LootFolder = ProjectFolder + "/Data/Loot/";
        const string MonstersFolder = ProjectFolder + "/Data/Monsters/";
        const string BossesFolder = ProjectFolder + "/Data/Bosses/";

        [MenuItem("Game/Set Up Main Scene")]
        public static void SetUp()
        {
            UpdateSheets();
            CreateScene();
            Debug.Log($"Main scene ready at {ScenePath}. Press Play.");
        }

        [MenuItem("Game/Update Sheets")]
        public static void UpdateSheets()
        {
            EnsureFolder(ProjectFolder, "Resources");
            var sheets = AssetDatabase.LoadAssetAtPath<GameSheets>(SheetsPath);
            if (sheets == null)
            {
                sheets = ScriptableObject.CreateInstance<GameSheets>();
                AssetDatabase.CreateAsset(sheets, SheetsPath);
            }

            sheets.itemBases = Sheet(LootFolder + LootSheets.BasesFile);
            sheets.affixes = Sheet(LootFolder + LootSheets.AffixesFile);
            sheets.runes = Sheet(LootFolder + LootSheets.RunesFile);
            sheets.runewords = Sheet(LootFolder + LootSheets.RunewordsFile);
            sheets.monsters = Sheet(MonstersFolder + MonsterSheet.File);
            sheets.bosses = Sheet(BossesFolder + BossSheet.BossesFile);
            sheets.bossAttacks = Sheet(BossesFolder + BossSheet.AttacksFile);
            EditorUtility.SetDirty(sheets);
            AssetDatabase.SaveAssets();
        }

        static void CreateScene()
        {
            EnsureFolder(ProjectFolder, "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);

            new GameObject("Game", typeof(GameRoot));

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static TextAsset Sheet(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) Debug.LogError($"Sheet not found: {path}");
            return asset;
        }

        internal static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
