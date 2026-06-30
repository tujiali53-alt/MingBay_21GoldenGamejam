using System.Collections.Generic;
using MingBay.Data;
using MingBay.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace MingBay.Editor
{
    public static class EndingSceneBuilder
    {
        private const string EndingScenePath = "Assets/Scenes/EndingScene.unity";
        private const string EndingDatabasePath = "Assets/Configs/EndingDatabase.asset";
        private const string SpreadsheetConfigJsonPath =
            "Assets/Configs/Spreadsheet/MingBaySpreadsheetConfig.json";
        private const string ChineseFontAssetPath = "Assets/UI/Fonts/NotoSansSC-Regular SDF.asset";

        [MenuItem("明湾/场景工具/生成结局场景")]
        public static void Build()
        {
            SpreadsheetConfig config = LoadSpreadsheetConfig();
            EndingDatabase database = CreateOrUpdateEndingDatabase(config);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            _ = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));

            GameObject controllerObject = new("EndingSceneController");
            EndingSceneView view = controllerObject.AddComponent<EndingSceneView>();
            SerializedObject serializedView = new(view);
            serializedView.FindProperty("endingDatabase").objectReferenceValue = database;
            serializedView.FindProperty("fontAsset").objectReferenceValue = LoadChineseFont();
            serializedView.FindProperty("titleSceneName").stringValue = "TitleScene";
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, EndingScenePath);
            AddEndingSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("EndingScene and EndingDatabase generated successfully.");
        }

        private static SpreadsheetConfig LoadSpreadsheetConfig()
        {
            TextAsset configAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(SpreadsheetConfigJsonPath);
            if (configAsset == null)
            {
                throw new System.InvalidOperationException(
                    $"Missing spreadsheet config at {SpreadsheetConfigJsonPath}.");
            }

            SpreadsheetConfig config =
                JsonUtility.FromJson<SpreadsheetConfig>(configAsset.text);
            if (config == null || config.endings == null || config.endings.Length == 0)
            {
                throw new System.InvalidOperationException(
                    "Spreadsheet config has no 13_EndingConfig data.");
            }

            return config;
        }

        private static EndingDatabase CreateOrUpdateEndingDatabase(SpreadsheetConfig config)
        {
            EndingDatabase database =
                AssetDatabase.LoadAssetAtPath<EndingDatabase>(EndingDatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<EndingDatabase>();
                AssetDatabase.CreateAsset(database, EndingDatabasePath);
            }

            List<EndingDefinition> endings = new();
            foreach (SpreadsheetEnding ending in config.endings)
            {
                if (ending == null || string.IsNullOrWhiteSpace(ending.endingId))
                {
                    continue;
                }

                endings.Add(
                    new EndingDefinition(
                        ending.endingId,
                        ending.priority,
                        ending.endingNameCn,
                        ending.conditionExpression,
                        ending.requiredStage,
                        ending.endingDescCn,
                        ending.finalScreenTextCn));
            }

            database.ReplaceEndings(endings);
            EditorUtility.SetDirty(database);
            return database;
        }

        private static TMP_FontAsset LoadChineseFont()
        {
            TMP_FontAsset font =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontAssetPath);
            if (font != null)
            {
                return font;
            }

            Debug.LogWarning(
                $"Missing Chinese TMP font asset at {ChineseFontAssetPath}. " +
                "EndingScene will fall back to TMP default font.");
            return TMP_Settings.defaultFontAsset;
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.06f, 0.06f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.tag = "MainCamera";
        }

        private static void AddEndingSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == EndingScenePath)
                {
                    scenes[i] = new EditorBuildSettingsScene(EndingScenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(EndingScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [System.Serializable]
        private sealed class SpreadsheetConfig
        {
            public SpreadsheetEnding[] endings;
        }

        [System.Serializable]
        private sealed class SpreadsheetEnding
        {
            public string endingId;
            public string endingNameCn;
            public int priority;
            public string conditionExpression;
            public string requiredStage;
            public string endingDescCn;
            public string finalScreenTextCn;
        }
    }
}
