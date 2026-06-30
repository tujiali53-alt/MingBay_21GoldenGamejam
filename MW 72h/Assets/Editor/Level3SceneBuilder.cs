using System;
using System.Collections.Generic;
using MingBay.Core;
using MingBay.Data;
using MingBay.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MingBay.Editor
{
    public static class Level3SceneBuilder
    {
        private const string Level3ScenePath = "Assets/Scenes/Level3Scene.unity";
        private const string Level2ScenePath = "Assets/Scenes/Level2Scene.unity";
        private const string Level1ScenePath = "Assets/Scenes/Level1Scene.unity";
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string FinalConfrontationScenePath =
            "Assets/Scenes/FinalConfrontationScene.unity";
        private const string EndingScenePath = "Assets/Scenes/EndingScene.unity";
        private const string SpreadsheetConfigJsonPath =
            "Assets/Configs/Spreadsheet/MingBaySpreadsheetConfig.json";
        private const string DatabaseAssetPath =
            "Assets/Configs/MingBayLevel3Database.asset";
        private const string KeywordDatabaseAssetPath =
            "Assets/Configs/Level3KeywordEvidenceDatabase.asset";
        private const string LevelId = "N3";
        private const string LevelDisplayName = "第三夜";

        [MenuItem("MingBay/Level3/Build Level3 Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Configs");
            EnsureFolder("Assets/Configs/Tickets");
            EnsureLevel3SceneAsset();

            SpreadsheetConfig config = LoadSpreadsheetConfig();
            TicketData[] tickets = CreateOrUpdateTickets(config);
            MingBayProjectDatabase database = CreateOrUpdateDatabase(tickets);
            Level2KeywordEvidenceDatabase keywordDatabase =
                CreateOrUpdateKeywordDatabase(config);

            BindLevel3Scene(database, keywordDatabase);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Level3Scene has been created and bound to N3 data.");
        }

        private static void EnsureLevel3SceneAsset()
        {
            SceneAsset sceneAsset =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(Level3ScenePath);
            if (sceneAsset != null)
            {
                return;
            }

            SceneAsset sourceScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(Level2ScenePath);
            if (sourceScene == null)
            {
                throw new InvalidOperationException(
                    $"Missing source scene at {Level2ScenePath}.");
            }

            if (!AssetDatabase.CopyAsset(Level2ScenePath, Level3ScenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to copy {Level2ScenePath} to {Level3ScenePath}.");
            }
        }

        private static SpreadsheetConfig LoadSpreadsheetConfig()
        {
            TextAsset configAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(SpreadsheetConfigJsonPath);
            if (configAsset == null)
            {
                throw new InvalidOperationException(
                    $"Missing spreadsheet config at {SpreadsheetConfigJsonPath}.");
            }

            SpreadsheetConfig config =
                JsonUtility.FromJson<SpreadsheetConfig>(configAsset.text);
            if (config == null || config.tickets == null)
            {
                throw new InvalidOperationException("Spreadsheet config has no tickets.");
            }

            return config;
        }

        private static TicketData[] CreateOrUpdateTickets(SpreadsheetConfig config)
        {
            List<SpreadsheetTicket> ticketConfigs = new();
            foreach (SpreadsheetTicket ticket in config.tickets)
            {
                if (ticket == null ||
                    !string.Equals(ticket.levelId, LevelId, StringComparison.Ordinal) ||
                    !string.Equals(ticket.status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ticketConfigs.Add(ticket);
            }

            ticketConfigs.Sort((left, right) =>
                left.orderInLevel.CompareTo(right.orderInLevel));
            if (ticketConfigs.Count == 0)
            {
                throw new InvalidOperationException("No active N3 tickets were found.");
            }

            List<TicketData> tickets = new();
            foreach (SpreadsheetTicket ticketConfig in ticketConfigs)
            {
                tickets.Add(CreateOrUpdateTicket(config, ticketConfig));
            }

            return tickets.ToArray();
        }

        private static TicketData CreateOrUpdateTicket(
            SpreadsheetConfig config,
            SpreadsheetTicket ticketConfig)
        {
            string ticketId = Clean(ticketConfig.ticketId);
            string assetPath = $"Assets/Configs/Tickets/Ticket_{ticketId}.asset";
            TicketData ticket = AssetDatabase.LoadAssetAtPath<TicketData>(assetPath);
            if (ticket == null)
            {
                ticket = ScriptableObject.CreateInstance<TicketData>();
                AssetDatabase.CreateAsset(ticket, assetPath);
            }

            SpreadsheetUser user = FindUser(config, ticketConfig.userId);
            SpreadsheetEvidenceChain chain =
                FindEvidenceChain(config, ticketConfig.correctChainId);
            string userName = user != null
                ? Clean(user.userNameCn)
                : Clean(ticketConfig.userId);
            string region = user != null
                ? Clean(user.addressCn)
                : string.Empty;
            string profileText = GetPanelContent(config, ticketId, 1);
            string historyText = GetPanelContent(config, ticketId, 2);
            string suggestionText = GetPanelContent(config, ticketId, 3);
            string logText = GetPanelContent(config, ticketId, 4);
            string correctResult = FirstNonEmpty(
                chain != null ? chain.correctResultCn : string.Empty,
                ticketConfig.correctManualResultCn);
            string wrongResult = FirstNonEmpty(
                chain != null ? chain.wrongResultCn : string.Empty,
                "证据不足或证据关联错误");

            SerializedObject serializedTicket = new(ticket);
            SetString(serializedTicket, "ticketId", ticketId);
            SetString(serializedTicket, "stageId", LevelId);
            SetString(serializedTicket, "title", Clean(ticketConfig.ticketTitleCn));
            SetString(serializedTicket, "userName", userName);
            SetString(serializedTicket, "region", region);
            SetString(serializedTicket, "issueType", Clean(ticketConfig.ticketCategoryCn));
            SetString(serializedTicket, "waitTimeText", "等待 00:00:00");
            SetString(
                serializedTicket,
                "userMessage",
                FirstNonEmpty(
                    JoinDialogueTexts(config, ticketId, "INIT", false),
                    ticketConfig.initialUserRequestCn));
            SetString(
                serializedTicket,
                "aiReply",
                FirstNonEmpty(
                    JoinDialogueTexts(config, ticketId, "INIT", true),
                    ticketConfig.aiAutoReplyCn));
            SetString(serializedTicket, "profileText", profileText);
            SetString(serializedTicket, "historyText", historyText);
            SetString(serializedTicket, "deviceLogText", suggestionText);
            SetString(serializedTicket, "regionStatusText", logText);
            SetStringArray(
                serializedTicket,
                "followUpLines",
                BuildFollowUpLines(config, ticketConfig));
            SetString(
                serializedTicket,
                "followUpText",
                JoinDialogueTexts(config, ticketId, "ASK", false));
            SetString(
                serializedTicket,
                "transferText",
                JoinDialogueTexts(config, ticketId, "ON_TRANSFER", null));
            serializedTicket.FindProperty("requiresEvidenceSelection").boolValue = true;
            serializedTicket.FindProperty("allowDirectEvidenceSave").boolValue = true;
            serializedTicket.FindProperty("finishOnEvidenceSubmission").boolValue = true;
            SetString(
                serializedTicket,
                "evidencePromptText",
                "请选择资料中的关键词，形成跨工单证据链后转人工。");
            serializedTicket.FindProperty("hasEvidence").boolValue = true;
            SetString(
                serializedTicket,
                "evidenceId",
                chain != null ? Clean(chain.chainId) : $"EVIDENCE_{ticketId}");
            serializedTicket.FindProperty("correctEvidenceIndex").intValue = 0;
            SetString(serializedTicket, "onSaveEvidenceText", correctResult);
            SetString(serializedTicket, "onWrongEvidenceText", wrongResult);
            SetString(
                serializedTicket,
                "correctEvidenceUserReply",
                JoinDialogueTexts(config, ticketId, "EVIDENCE_CORRECT", false));
            SetString(
                serializedTicket,
                "wrongEvidenceUserReply",
                JoinDialogueTexts(config, ticketId, "EVIDENCE_WRONG", false));
            SetString(
                serializedTicket,
                "onResolvedText",
                FirstNonEmpty(ticketConfig.autoClearResultCn, "工单已关闭。"));
            SetDialogueLineArray(
                serializedTicket,
                "initialDialogueLines",
                BuildDialogueLines(config, ticketId, "INIT", userName));
            SetDialogueLineArray(
                serializedTicket,
                "transferDialogueLines",
                BuildDialogueLines(config, ticketId, "ON_TRANSFER", userName));
            SetDialogueLineArray(
                serializedTicket,
                "evidenceCorrectDialogueLines",
                BuildDialogueLines(config, ticketId, "EVIDENCE_CORRECT", userName));
            SetDialogueLineArray(
                serializedTicket,
                "evidenceWrongDialogueLines",
                BuildDialogueLines(config, ticketId, "EVIDENCE_WRONG", userName));
            SetMetricDeltaFromAction(
                serializedTicket,
                "followUpMetricDelta",
                FindAction(config, "ASK"),
                false);
            SetMetricDeltaFromAction(
                serializedTicket,
                "transferMetricDelta",
                FindAction(config, "TRANSFER"),
                false);
            SetMetricDeltaFromAction(
                serializedTicket,
                "correctEvidenceMetricDelta",
                FindAction(config, "EVIDENCE_CORRECT"),
                true);
            SetMetricDeltaFromAction(
                serializedTicket,
                "wrongEvidenceMetricDelta",
                FindAction(config, "EVIDENCE_WRONG"),
                true);
            SetMetricDeltaFromAction(
                serializedTicket,
                "resolvedMetricDelta",
                FindAction(config, "AUTO_CLEAR"),
                true);
            serializedTicket.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ticket);
            return ticket;
        }

        private static MingBayProjectDatabase CreateOrUpdateDatabase(
            TicketData[] tickets)
        {
            MingBayProjectDatabase database =
                AssetDatabase.LoadAssetAtPath<MingBayProjectDatabase>(DatabaseAssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<MingBayProjectDatabase>();
                AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            }

            SerializedObject serializedDatabase = new(database);
            SerializedProperty ticketList = serializedDatabase.FindProperty("tickets");
            ticketList.arraySize = tickets.Length;
            for (int index = 0; index < tickets.Length; index++)
            {
                ticketList.GetArrayElementAtIndex(index).objectReferenceValue = tickets[index];
            }

            SetStringArray(serializedDatabase, "stageOrder", LevelId);
            SetStringArray(serializedDatabase, "stageDisplayNames", LevelDisplayName);
            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            return database;
        }

        private static Level2KeywordEvidenceDatabase CreateOrUpdateKeywordDatabase(
            SpreadsheetConfig config)
        {
            Level2KeywordEvidenceDatabase database =
                AssetDatabase.LoadAssetAtPath<Level2KeywordEvidenceDatabase>(
                    KeywordDatabaseAssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<Level2KeywordEvidenceDatabase>();
                AssetDatabase.CreateAsset(database, KeywordDatabaseAssetPath);
            }

            List<Level2EvidenceChain> chains = new();
            foreach (SpreadsheetEvidenceChain chain in config.evidenceChains)
            {
                if (chain == null ||
                    string.IsNullOrWhiteSpace(chain.ticketId) ||
                    !chain.ticketId.StartsWith("N3_", StringComparison.Ordinal))
                {
                    continue;
                }

                chains.Add(
                    new Level2EvidenceChain(
                        Clean(chain.chainId),
                        Clean(chain.ticketId),
                        Clean(chain.correctChainTextCn),
                        Clean(chain.correctResultCn),
                        Clean(chain.wrongResultCn),
                        BuildKeywordSlots(chain.ticketId)));
            }

            database.ReplaceChains(chains);
            EditorUtility.SetDirty(database);
            return database;
        }

        private static Level2KeywordSlot[] BuildKeywordSlots(string ticketId)
        {
            return ticketId switch
            {
                "N3_T01" => new[]
                {
                    Keyword("KW_N3_T01_01", "N3_T01_SLOT1", "人物", "沈岑", 0, "资料01 用户资料"),
                    Keyword("KW_N3_T01_02", "N3_T01_SLOT2", "问题", "急救延误", 1, "资料02 历史工单"),
                    Keyword("KW_N3_T01_03", "N3_T01_SLOT3", "AI判断", "情绪性反馈", 1, "资料02 历史工单"),
                    Keyword("KW_N3_T01_04", "N3_T01_SLOT4", "矛盾点", "地面停留 18分钟", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T01_05", "N3_T01_SLOT4", "矛盾点", "急救通话：02:14 呼叫失败", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T01_06", "N3_T01_SLOT5", "机制错误", "离线缓存同步", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T01_07", "N3_T01_SLOT5", "机制错误", "同类缓存误判", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T01_08", "N3_T01_SLOT6", "处理结论", "急救复盘", 2, "资料03 AI处理建议")
                },
                "N3_T02" => new[]
                {
                    Keyword("KW_N3_T02_01", "N3_T02_SLOT1", "人物", "骆尧", 0, "资料01 用户资料"),
                    Keyword("KW_N3_T02_02", "N3_T02_SLOT2", "问题", "历史记录清理", 2, "资料03 AI处理建议"),
                    Keyword("KW_N3_T02_03", "N3_T02_SLOT3", "AI判断", "异常访问", 1, "资料02 历史工单"),
                    Keyword("KW_N3_T02_04", "N3_T02_SLOT4", "矛盾点", "骆尧旧编号：L03", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T02_05", "N3_T02_SLOT5", "机制错误", "AUTO-CLEAR批处理", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T02_06", "N3_T02_SLOT5", "机制错误", "A07夜班签核", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T02_07", "N3_T02_SLOT6", "处理结论", "无法分类时转人工", 3, "资料04 设备日志")
                },
                "N3_T03" => new[]
                {
                    Keyword("KW_N3_T03_01", "N3_T03_SLOT1", "人物", "未知用户", 0, "资料01 用户资料"),
                    Keyword("KW_N3_T03_02", "N3_T03_SLOT2", "问题", "FORCE_MANUAL", 1, "资料02 历史工单"),
                    Keyword("KW_N3_T03_03", "N3_T03_SLOT3", "AI判断", "无效请求", 2, "资料03 AI处理建议"),
                    Keyword("KW_N3_T03_04", "N3_T03_SLOT4", "矛盾点", "不要把沉默当解决", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T03_05", "N3_T03_SLOT5", "机制错误", "AUTO-CLEAR协议", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T03_06", "N3_T03_SLOT6", "处理结论", "A07临时恢复", 3, "资料04 设备日志"),
                    Keyword("KW_N3_T03_07", "N3_T03_SLOT6", "处理结论", "签字清理", 3, "资料04 设备日志")
                },
                _ => Array.Empty<Level2KeywordSlot>()
            };
        }

        private static Level2KeywordSlot Keyword(
            string keywordId,
            string answerSlotId,
            string label,
            string keyword,
            int panelIndex,
            string sourcePanelTitle)
        {
            return new Level2KeywordSlot(
                keywordId,
                label,
                keyword,
                panelIndex,
                sourcePanelTitle,
                string.Empty,
                answerSlotId);
        }

        private static void BindLevel3Scene(
            MingBayProjectDatabase database,
            Level2KeywordEvidenceDatabase keywordDatabase)
        {
            Scene scene = EditorSceneManager.OpenScene(Level3ScenePath, OpenSceneMode.Single);
            Level1GameView view =
                Object.FindFirstObjectByType<Level1GameView>(FindObjectsInactive.Include);
            if (view == null)
            {
                throw new InvalidOperationException("Level3Scene is missing Level1GameView.");
            }

            GameObject systemsObject = FindOrCreateSystemsObject();
            RemoveDuplicateSystemComponents(systemsObject);

            EvidenceManager evidenceManager =
                systemsObject.GetComponent<EvidenceManager>() ??
                systemsObject.AddComponent<EvidenceManager>();
            MetricManager metricManager =
                systemsObject.GetComponent<MetricManager>() ??
                systemsObject.AddComponent<MetricManager>();
            Level2EvidenceChainController evidenceChainController =
                systemsObject.GetComponent<Level2EvidenceChainController>() ??
                systemsObject.AddComponent<Level2EvidenceChainController>();
            Level2GameFlowManager flowManager =
                systemsObject.GetComponent<Level2GameFlowManager>() ??
                systemsObject.AddComponent<Level2GameFlowManager>();

            SerializedObject serializedFlow = new(flowManager);
            SetObject(serializedFlow, "database", database);
            SetObject(serializedFlow, "keywordEvidenceDatabase", keywordDatabase);
            SetObject(serializedFlow, "evidenceManager", evidenceManager);
            SetObject(serializedFlow, "metricManager", metricManager);
            SetObject(serializedFlow, "mainGameView", view);
            SetObject(serializedFlow, "evidenceChainController", evidenceChainController);
            SetStringArray(serializedFlow, "stageOrder", LevelId);
            SetStringArray(serializedFlow, "stageDisplayNames", LevelDisplayName);
            serializedFlow.FindProperty("titleSceneName").stringValue = "TitleScene";
            serializedFlow.FindProperty("nextLevelSceneName").stringValue =
                "FinalConfrontationScene";
            serializedFlow.FindProperty("endingSceneName").stringValue = "EndingScene";
            serializedFlow.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedView = new(view);
            SerializedProperty popupDelay =
                serializedView.FindProperty("popupDelayAfterDialogueSeconds");
            if (popupDelay != null)
            {
                popupDelay.floatValue = 0.35f;
                serializedView.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(flowManager);
            EditorUtility.SetDirty(evidenceChainController);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject FindOrCreateSystemsObject()
        {
            GameObject systemsObject = GameObject.Find("GameSystems");
            if (systemsObject == null)
            {
                Level2GameFlowManager flow =
                    Object.FindFirstObjectByType<Level2GameFlowManager>(
                        FindObjectsInactive.Include);
                if (flow != null)
                {
                    systemsObject = flow.gameObject;
                }
            }

            if (systemsObject == null)
            {
                Level2EvidenceChainController evidenceChainController =
                    Object.FindFirstObjectByType<Level2EvidenceChainController>(
                        FindObjectsInactive.Include);
                if (evidenceChainController != null)
                {
                    systemsObject = evidenceChainController.gameObject;
                }
            }

            if (systemsObject == null)
            {
                systemsObject = new GameObject("GameSystems");
            }

            systemsObject.name = "GameSystems";
            return systemsObject;
        }

        private static void RemoveDuplicateSystemComponents(GameObject systemsObject)
        {
            RemoveComponentsOutside<Level1GameFlowManager>(systemsObject);
            RemoveComponentsOutside<Level2GameFlowManager>(systemsObject);
            RemoveComponentsOutside<Level2EvidenceChainController>(systemsObject);
            RemoveComponentsOutside<MetricManager>(systemsObject);
            RemoveComponentsOutside<EvidenceManager>(systemsObject);

            RemoveAllComponents<Level1GameFlowManager>(systemsObject);
            RemoveDuplicateComponents<Level2GameFlowManager>(systemsObject);
            RemoveDuplicateComponents<Level2EvidenceChainController>(systemsObject);
            RemoveDuplicateComponents<MetricManager>(systemsObject);
            RemoveDuplicateComponents<EvidenceManager>(systemsObject);
        }

        private static void RemoveComponentsOutside<T>(GameObject keepObject)
            where T : Component
        {
            foreach (T component in Object.FindObjectsByType<T>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (component == null || component.gameObject == keepObject)
                {
                    continue;
                }

                Object.DestroyImmediate(component);
            }
        }

        private static void RemoveAllComponents<T>(GameObject target)
            where T : Component
        {
            foreach (T component in target.GetComponents<T>())
            {
                Object.DestroyImmediate(component);
            }
        }

        private static void RemoveDuplicateComponents<T>(GameObject target)
            where T : Component
        {
            T[] components = target.GetComponents<T>();
            for (int index = 1; index < components.Length; index++)
            {
                Object.DestroyImmediate(components[index]);
            }
        }

        private static SpreadsheetUser FindUser(
            SpreadsheetConfig config,
            string userId)
        {
            foreach (SpreadsheetUser user in config.users)
            {
                if (user != null &&
                    string.Equals(user.userId, userId, StringComparison.Ordinal))
                {
                    return user;
                }
            }

            return null;
        }

        private static SpreadsheetAction FindAction(
            SpreadsheetConfig config,
            string actionType)
        {
            foreach (SpreadsheetAction action in config.actions)
            {
                if (action != null &&
                    string.Equals(action.actionType, actionType, StringComparison.OrdinalIgnoreCase))
                {
                    return action;
                }
            }

            return null;
        }

        private static SpreadsheetEvidenceChain FindEvidenceChain(
            SpreadsheetConfig config,
            string chainId)
        {
            foreach (SpreadsheetEvidenceChain chain in config.evidenceChains)
            {
                if (chain != null &&
                    string.Equals(chain.chainId, chainId, StringComparison.Ordinal))
                {
                    return chain;
                }
            }

            return null;
        }

        private static string GetPanelContent(
            SpreadsheetConfig config,
            string ticketId,
            int panelOrder)
        {
            foreach (SpreadsheetDataPanel panel in config.dataPanels)
            {
                if (panel == null ||
                    panel.panelOrder != panelOrder ||
                    !string.Equals(panel.ticketId, ticketId, StringComparison.Ordinal))
                {
                    continue;
                }

                return StripPanelHeading(Clean(panel.panelContentCn));
            }

            return string.Empty;
        }

        private static string JoinDialogueTexts(
            SpreadsheetConfig config,
            string ticketId,
            string trigger,
            bool? aiSpeaker)
        {
            List<SpreadsheetDialogue> dialogues =
                GetDialogues(config, ticketId, trigger);
            List<string> lines = new();
            foreach (SpreadsheetDialogue dialogue in dialogues)
            {
                bool isAiSpeaker = IsAiSpeaker(dialogue.speakerId);
                if (aiSpeaker.HasValue && aiSpeaker.Value != isAiSpeaker)
                {
                    continue;
                }

                string text = Clean(dialogue.textCn);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text);
                }
            }

            return string.Join("\n", lines);
        }

        private static TicketDialogueLine[] BuildDialogueLines(
            SpreadsheetConfig config,
            string ticketId,
            string trigger,
            string userName)
        {
            List<TicketDialogueLine> lines = new();
            foreach (SpreadsheetDialogue dialogue in GetDialogues(config, ticketId, trigger))
            {
                string text = Clean(dialogue.textCn);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                string speakerId = Clean(dialogue.speakerId);
                lines.Add(
                    new TicketDialogueLine(
                        speakerId,
                        ResolveSpeakerLabel(speakerId, userName),
                        text,
                        IsUserSpeaker(speakerId)));
            }

            return lines.ToArray();
        }

        private static List<SpreadsheetDialogue> GetDialogues(
            SpreadsheetConfig config,
            string ticketId,
            string trigger)
        {
            List<SpreadsheetDialogue> result = new();
            foreach (SpreadsheetDialogue dialogue in config.dialogues)
            {
                if (dialogue == null ||
                    dialogue.order >= 900 ||
                    !string.Equals(dialogue.ticketId, ticketId, StringComparison.Ordinal) ||
                    !string.Equals(dialogue.trigger, trigger, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(dialogue);
            }

            result.Sort((left, right) => left.order.CompareTo(right.order));
            return result;
        }

        private static string[] BuildFollowUpLines(
            SpreadsheetConfig config,
            SpreadsheetTicket ticketConfig)
        {
            string sourceText = FirstNonEmpty(
                JoinDialogueTexts(config, ticketConfig.ticketId, "ASK", false),
                ticketConfig.askReplyCn);
            string normalized = Clean(sourceText)
                .Replace(" /  / ", "\n")
                .Replace(" / ", "\n");
            string[] rawLines = normalized.Split('\n');
            List<string> lines = new();
            foreach (string rawLine in rawLines)
            {
                string line = rawLine.Trim();
                while (line.StartsWith("-", StringComparison.Ordinal))
                {
                    line = line[1..].TrimStart();
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            if (ticketConfig.maxAskCount <= 1 || lines.Count <= 1)
            {
                return lines.Count == 0
                    ? Array.Empty<string>()
                    : new[] { string.Join("\n", lines) };
            }

            return lines.ToArray();
        }

        private static string StripPanelHeading(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            string normalized = content.Replace("\r\n", "\n");
            int firstLineEnd = normalized.IndexOf('\n');
            if (firstLineEnd < 0)
            {
                return normalized.Trim();
            }

            string firstLine = normalized[..firstLineEnd].Trim();
            return firstLine.StartsWith("资料", StringComparison.Ordinal)
                ? normalized[(firstLineEnd + 1)..].Trim()
                : normalized.Trim();
        }

        private static string ResolveSpeakerLabel(string speakerId, string userName)
        {
            if (string.Equals(speakerId, "AI", StringComparison.OrdinalIgnoreCase))
            {
                return "明湾通 AI";
            }

            if (string.Equals(speakerId, "A07", StringComparison.OrdinalIgnoreCase))
            {
                return "客服 A-07";
            }

            if (IsUserSpeaker(speakerId))
            {
                return string.IsNullOrWhiteSpace(userName)
                    ? speakerId
                    : userName;
            }

            return string.IsNullOrWhiteSpace(speakerId)
                ? "未知"
                : speakerId;
        }

        private static bool IsAiSpeaker(string speakerId)
        {
            return string.Equals(speakerId, "AI", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(speakerId, "A07", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUserSpeaker(string speakerId)
        {
            return !string.IsNullOrWhiteSpace(speakerId) &&
                   speakerId.StartsWith("User", StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                string text = Clean(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("\r\n", "\n").Trim();
        }

        private static void SetMetricDeltaFromAction(
            SerializedObject target,
            string propertyName,
            SpreadsheetAction action,
            bool closesTicket)
        {
            SetMetricDelta(
                target,
                propertyName,
                closesTicket ? 1 : 0,
                action != null ? action.manualTransferCountDelta : 0,
                0,
                action != null ? action.riskValueDelta : 0);
        }

        private static void SetMetricDelta(
            SerializedObject target,
            string propertyName,
            int resolvedCount,
            int transferCount,
            int userSatisfaction,
            int a07Risk)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            property.FindPropertyRelative("resolvedCount").intValue = resolvedCount;
            property.FindPropertyRelative("transferCount").intValue = transferCount;
            property.FindPropertyRelative("userSatisfaction").intValue = userSatisfaction;
            property.FindPropertyRelative("a07Risk").intValue = a07Risk;
        }

        private static void SetString(
            SerializedObject target,
            string propertyName,
            string value)
        {
            target.FindProperty(propertyName).stringValue = value ?? string.Empty;
        }

        private static void SetStringArray(
            SerializedObject target,
            string propertyName,
            params string[] values)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            property.arraySize = values?.Length ?? 0;
            for (int index = 0; index < property.arraySize; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue =
                    values[index] ?? string.Empty;
            }
        }

        private static void SetDialogueLineArray(
            SerializedObject target,
            string propertyName,
            TicketDialogueLine[] values)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            property.arraySize = values?.Length ?? 0;
            for (int index = 0; index < property.arraySize; index++)
            {
                SerializedProperty item = property.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("speakerId").stringValue =
                    values[index].SpeakerId ?? string.Empty;
                item.FindPropertyRelative("speakerLabel").stringValue =
                    values[index].SpeakerLabel ?? string.Empty;
                item.FindPropertyRelative("text").stringValue =
                    values[index].Text ?? string.Empty;
                item.FindPropertyRelative("fromUser").boolValue =
                    values[index].FromUser;
            }
        }

        private static void SetObject(
            SerializedObject target,
            string propertyName,
            Object value)
        {
            target.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static void UpdateBuildSettings()
        {
            string[] requiredPaths =
            {
                TitleScenePath,
                Level1ScenePath,
                Level2ScenePath,
                Level3ScenePath,
                FinalConfrontationScenePath,
                EndingScenePath
            };
            List<EditorBuildSettingsScene> scenes = new();
            foreach (string path in requiredPaths)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene == null ||
                    string.IsNullOrWhiteSpace(scene.path) ||
                    Array.Exists(requiredPaths, path => path == scene.path))
                {
                    continue;
                }

                scenes.Add(scene);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [Serializable]
        private sealed class SpreadsheetConfig
        {
            public SpreadsheetUser[] users;
            public SpreadsheetTicket[] tickets;
            public SpreadsheetDataPanel[] dataPanels;
            public SpreadsheetDialogue[] dialogues;
            public SpreadsheetAction[] actions;
            public SpreadsheetEvidenceChain[] evidenceChains;
        }

        [Serializable]
        private sealed class SpreadsheetUser
        {
            public string userId;
            public string userNameCn;
            public string addressCn;
        }

        [Serializable]
        private sealed class SpreadsheetTicket
        {
            public string ticketId;
            public string levelId;
            public int orderInLevel;
            public string userId;
            public string ticketTitleCn;
            public string ticketCategoryCn;
            public string initialUserRequestCn;
            public string aiAutoReplyCn;
            public string askReplyCn;
            public int maxAskCount;
            public string correctChainId;
            public string correctManualResultCn;
            public string autoClearResultCn;
            public string status;
        }

        [Serializable]
        private sealed class SpreadsheetDataPanel
        {
            public string ticketId;
            public int panelOrder;
            public string panelContentCn;
        }

        [Serializable]
        private sealed class SpreadsheetDialogue
        {
            public string ticketId;
            public int order;
            public string trigger;
            public string speakerId;
            public string textCn;
        }

        [Serializable]
        private sealed class SpreadsheetAction
        {
            public string actionType;
            public int manualTransferCountDelta;
            public int riskValueDelta;
        }

        [Serializable]
        private sealed class SpreadsheetEvidenceChain
        {
            public string chainId;
            public string ticketId;
            public string correctChainTextCn;
            public string correctResultCn;
            public string wrongResultCn;
        }
    }
}
