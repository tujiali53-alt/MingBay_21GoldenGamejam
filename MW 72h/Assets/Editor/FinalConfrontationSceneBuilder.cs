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
    public static class FinalConfrontationSceneBuilder
    {
        private const string FinalScenePath = "Assets/Scenes/FinalConfrontationScene.unity";
        private const string FinalDatabasePath =
            "Assets/Configs/FinalConfrontationDatabase.asset";
        private const string SpreadsheetConfigJsonPath =
            "Assets/Configs/Spreadsheet/MingBaySpreadsheetConfig.json";
        private const string ChineseFontAssetPath = "Assets/UI/Fonts/NotoSansSC-Regular SDF.asset";

        [MenuItem("明湾/场景工具/生成最终对峙场景")]
        public static void Build()
        {
            SpreadsheetConfig config = LoadSpreadsheetConfig();
            FinalConfrontationDatabase database = CreateOrUpdateDatabase(config);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            _ = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));

            GameObject controllerObject = new("FinalConfrontationSceneController");
            FinalConfrontationSceneView view =
                controllerObject.AddComponent<FinalConfrontationSceneView>();
            SerializedObject serializedView = new(view);
            serializedView.FindProperty("database").objectReferenceValue = database;
            serializedView.FindProperty("fontAsset").objectReferenceValue = LoadChineseFont();
            serializedView.FindProperty("endingSceneName").stringValue = "EndingScene";
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, FinalScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("FinalConfrontationScene and FinalConfrontationDatabase generated successfully.");
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
            if (config == null ||
                config.finalConfrontations == null ||
                config.finalConfrontations.Length == 0)
            {
                throw new System.InvalidOperationException(
                    "Spreadsheet config has no 14_FinalConfrontation data.");
            }

            return config;
        }

        private static FinalConfrontationDatabase CreateOrUpdateDatabase(
            SpreadsheetConfig config)
        {
            FinalConfrontationDatabase database =
                AssetDatabase.LoadAssetAtPath<FinalConfrontationDatabase>(FinalDatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<FinalConfrontationDatabase>();
                AssetDatabase.CreateAsset(database, FinalDatabasePath);
            }

            List<FinalConfrontationQuestion> questions = new();
            foreach (SpreadsheetFinalConfrontation row in config.finalConfrontations)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.confrontationId))
                {
                    continue;
                }

                questions.Add(
                    new FinalConfrontationQuestion(
                        row.confrontationId,
                        row.stepIndex,
                        row.systemStatementCn,
                        row.playerGoalCn,
                        row.correctEvidenceChainId,
                        row.selectableEvidenceChainIds,
                        row.successReplyCn,
                        row.failReplyCn,
                        row.allowRetry));
            }

            List<FinalEvidenceOption> evidenceOptions = new();
            if (config.evidenceChains != null)
            {
                foreach (SpreadsheetEvidenceChain chain in config.evidenceChains)
                {
                    if (chain == null || string.IsNullOrWhiteSpace(chain.chainId))
                    {
                        continue;
                    }

                    evidenceOptions.Add(
                        new FinalEvidenceOption(
                            chain.chainId,
                            chain.correctChainTextCn));
                }
            }

            database.ReplaceContent(questions, evidenceOptions);
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
                "FinalConfrontationScene will fall back to TMP default font.");
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

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == FinalScenePath)
                {
                    scenes[i] = new EditorBuildSettingsScene(FinalScenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(FinalScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [System.Serializable]
        private sealed class SpreadsheetConfig
        {
            public SpreadsheetFinalConfrontation[] finalConfrontations;
            public SpreadsheetEvidenceChain[] evidenceChains;
        }

        [System.Serializable]
        private sealed class SpreadsheetFinalConfrontation
        {
            public string confrontationId;
            public int stepIndex;
            public string systemStatementCn;
            public string playerGoalCn;
            public string correctEvidenceChainId;
            public string selectableEvidenceChainIds;
            public string successReplyCn;
            public string failReplyCn;
            public bool allowRetry;
        }

        [System.Serializable]
        private sealed class SpreadsheetEvidenceChain
        {
            public string chainId;
            public string correctChainTextCn;
        }
    }
}
