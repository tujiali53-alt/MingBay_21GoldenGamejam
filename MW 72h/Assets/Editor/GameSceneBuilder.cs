using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MingBay.Core;
using MingBay.Data;
using MingBay.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MingBay.Editor
{
    /// <summary>
    /// 基础工单队列场景生成工具。
    /// 所有美术位置仅使用 Unity 默认 Image、色块和文字占位，方便后续直接替换。
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private static readonly string[] TicketAssetPaths =
        {
            "Assets/Configs/Tickets/Ticket_T_S01_001.asset",
            "Assets/Configs/Tickets/Ticket_T_S01_002.asset",
            "Assets/Configs/Tickets/Ticket_T_D01_001.asset",
            "Assets/Configs/Tickets/Ticket_T_D01_002.asset",
            "Assets/Configs/Tickets/Ticket_T_D01_003.asset",
            "Assets/Configs/Tickets/Ticket_T_D01_004.asset"
        };
        private static readonly string[] LegacyTicketAssetPaths =
        {
            "Assets/Configs/Tickets/Ticket_T_001.asset",
            "Assets/Configs/Tickets/Ticket_T_002.asset",
            "Assets/Configs/Tickets/Ticket_T_003.asset",
            "Assets/Configs/Tickets/Ticket_T_004.asset"
        };
        private const string DatabaseAssetPath = "Assets/Configs/DemoDatabase.asset";
        private const string ChineseFontPath =
            "Assets/UI/Fonts/NotoSansSC-Regular SDF.asset";
        private const string ChineseSourceFontPath =
            "Assets/UI/Fonts/NotoSansSC-Regular.ttf";

        // 使用中性灰阶作为默认占位配色，后续美术可直接替换对应 Image。
        private static readonly Color Background = Hex("202020");
        private static readonly Color TopBar = Hex("282828");
        private static readonly Color Panel = Hex("353535");
        private static readonly Color PanelRaised = Hex("4A4A4A");
        private static readonly Color PanelDark = Hex("2A2A2A");
        private static readonly Color Accent = Hex("808080");
        private static readonly Color AccentHover = Hex("A0A0A0");
        private static readonly Color Warning = Hex("969696");
        private static readonly Color PrimaryText = Hex("EFEFEF");
        private static readonly Color MutedText = Hex("A8A8A8");
        private static readonly Color Border = new(0.55f, 0.55f, 0.55f, 0.28f);

        /// <summary>
        /// 重新创建测试工单、Demo 数据库和 GameScene。
        /// 注意：执行后会覆盖当前 GameScene。
        /// </summary>
        [MenuItem("明湾/场景工具/生成基础工单 Demo")]
        public static void Build()
        {
            EnsureFolder("Assets/Configs/Tickets");
            EnsureFolder("Assets/UI");
            MigrateLegacyTicketAssets();

            TMP_FontAsset font = EnsureChineseFont();
            TicketData[] tickets = CreateOrUpdateTickets();
            DemoDatabase database = CreateOrUpdateDatabase(tickets);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera();
            CreateEventSystem();

            GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            CreateImage("Background", root, Background, Vector2.zero, Vector2.one);
            CreateTopBar(root, font, out TMP_Text progressText, out TMP_Text statusText,
                out TMP_Text evidenceText, out TMP_Text resolvedText);
            CreateQueuePanel(
                root,
                font,
                out RectTransform queueContent,
                out TicketQueueItemView queueItemTemplate);
            RectTransform ticketContentRoot = CreateRect(
                "TicketContentRoot",
                root,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            CreateTicketPanel(ticketContentRoot, font, out TMP_Text titleText, out TMP_Text metaText,
                out TMP_Text userMessageText, out TMP_Text aiReplyText);
            CreateDataArea(ticketContentRoot, font, out GameObject dataPanel, out TMP_Text profileText,
                out TMP_Text historyText, out TMP_Text deviceLogText, out TMP_Text regionStatusText);
            CreateActionBar(ticketContentRoot, font, out Button primaryActionButton, out Button transferHumanButton,
                out Button markResolvedButton);
            CreateResultPanel(root, font, out GameObject resultPanel, out TMP_Text resultTitleText,
                out TMP_Text resultDescriptionText, out TMP_Text resultMetricsText,
                out Button resultActionButton);

            // 先保持对象关闭，避免 AddComponent 时触发尚未绑定引用的 OnEnable。
            GameObject viewObject = new("MainGameView");
            viewObject.SetActive(false);
            MainGameView view = viewObject.AddComponent<MainGameView>();
            BindView(
                view,
                queueContent,
                queueItemTemplate,
                new[] { ticketContentRoot.gameObject },
                progressText,
                statusText,
                evidenceText,
                resolvedText,
                titleText,
                metaText,
                userMessageText,
                aiReplyText,
                dataPanel,
                profileText,
                historyText,
                deviceLogText,
                regionStatusText,
                primaryActionButton,
                transferHumanButton,
                markResolvedButton,
                resultPanel,
                resultTitleText,
                resultDescriptionText,
                resultMetricsText,
                resultActionButton);
            viewObject.SetActive(true);

            GameObject systemsObject = new("GameSystems");
            systemsObject.SetActive(false);
            EvidenceManager evidenceManager = systemsObject.AddComponent<EvidenceManager>();
            MetricManager metricManager = systemsObject.AddComponent<MetricManager>();
            GameFlowManager flowManager = systemsObject.AddComponent<GameFlowManager>();
            BindFlowManager(flowManager, database, evidenceManager, metricManager, view);
            systemsObject.SetActive(true);

            // 保存前生成队列预览；正式运行时 GameFlowManager 会清理并重新生成。
            List<TicketData> tutorialTickets = database.GetTicketsByStage("Stage_Tutorial");
            view.BuildTicketQueue(tutorialTickets);
            view.ShowTicketSelection(
                "教程关卡",
                0,
                tutorialTickets.Count,
                new GameMetrics(0, 0, 0, 0, 0),
                new bool[tutorialTickets.Count]);

            EditorSceneManager.SaveScene(scene, GameScenePath);
            UpdateBuildSettings();
            ValidateGeneratedContent(database, flowManager, view, queueItemTemplate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CapturePreview(canvas, camera);

            Debug.Log("GameScene 基础工单队列 Demo 生成成功。");
        }

        /// <summary>
        /// 检查生成后的核心数据与 Inspector 引用，避免场景可以保存但运行时因漏绑而报错。
        /// </summary>
        private static void ValidateGeneratedContent(
            DemoDatabase database,
            GameFlowManager flowManager,
            MainGameView view,
            TicketQueueItemView queueItemTemplate)
        {
            if (database == null || database.TicketCount <= 0 || database.GetTicket(0) == null)
            {
                throw new InvalidOperationException("DemoDatabase 至少需要包含一张有效工单。");
            }

            List<TicketData> tutorialTickets = database.GetTicketsByStage("Stage_Tutorial");
            List<TicketData> dayOneTickets = database.GetTicketsByStage("Stage_Day1");
            if (tutorialTickets.Count != 2 || dayOneTickets.Count != 4)
            {
                throw new InvalidOperationException(
                    "新版流程必须包含 2 张教程工单和 4 张第一天正式工单。");
            }

            HashSet<string> ticketIds = new();
            foreach (TicketData ticket in database.Tickets)
            {
                if (ticket == null ||
                    string.IsNullOrWhiteSpace(ticket.TicketId) ||
                    !ticketIds.Add(ticket.TicketId))
                {
                    throw new InvalidOperationException(
                        "DemoDatabase 中存在空工单、空工单 ID 或重复工单 ID。");
                }
            }

            ValidateObjectReferences(flowManager, nameof(GameFlowManager));
            ValidateObjectReferences(view, nameof(MainGameView));
            ValidateObjectReferences(queueItemTemplate, nameof(TicketQueueItemView));

            bool hasTitleScene = Array.Exists(
                EditorBuildSettings.scenes,
                scene => scene.enabled && scene.path == TitleScenePath);
            bool hasGameScene = Array.Exists(
                EditorBuildSettings.scenes,
                scene => scene.enabled && scene.path == GameScenePath);

            if (!hasTitleScene || !hasGameScene)
            {
                throw new InvalidOperationException(
                    "Build Settings 必须同时包含启用状态的 TitleScene 与 GameScene。");
            }
        }

        /// <summary>
        /// 检查组件上所有序列化对象引用是否已绑定。
        /// </summary>
        private static void ValidateObjectReferences(UnityEngine.Object target, string targetName)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.propertyPath == "m_Script")
                {
                    continue;
                }

                if (property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"{targetName} 的 Inspector 引用“{property.displayName}”尚未绑定。");
                }
            }
        }

        private static TicketData[] CreateOrUpdateTickets()
        {
            return new[]
            {
                CreateOrUpdateTicket(
                    TicketAssetPaths[0],
                    "T_S01_001",
                    "Stage_Tutorial",
                    "门禁卡失效",
                    "老张",
                    "东区 3 号楼",
                    "门禁故障",
                    "等待 00:12:40",
                    "有人在管东区 3 号楼吗？门禁什么情况，刷了三次还不开。",
                    "A07：已检测到您的需求，请确认门禁卡上周完成区域反注册，并优先申请该楼层同步。",
                    "用户：老张\n住址：东区 3 号楼\n账户状态：正常\n门禁卡：有效",
                    "同楼居民一周前反馈过相同问题，转人工后已处理。",
                    "建议用户确认门禁卡状态并等待自动重连。",
                    "22:14 门禁链路断开；自动重连失败；近郊多次门禁短断。",
                    true,
                    "E_TUTORIAL_DOOR",
                    "A07：后台记录确认东区 3 号楼门禁链路中断。证据有效，本次工单已进入后续核验。",
                    "A07：该资料与门禁故障没有直接关联，证据无效。",
                    "工单已关闭。教程提示：直接关闭可能无法解决居民问题。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[1],
                    "T_S01_002",
                    "Stage_Tutorial",
                    "自动续费未提醒，要求退款",
                    "赵宁",
                    "明湾西区",
                    "订阅退款",
                    "等待 00:08:12",
                    "我想问一下，明湾云端服务为什么自动续费？没有短信也没有弹窗提醒。",
                    "平台已根据服务规范自动完成续费。经查询，服务处于正常续费状态。",
                    "用户：赵宁\n账户状态：正常\n订阅状态：已续费",
                    "扣款前没有短信、邮件或应用内弹窗提醒。",
                    "建议确认用户是否阅读自动续费协议。",
                    "扣款时间正常；提醒发送记录为空。",
                    false,
                    string.Empty,
                    "当前工单没有需要提交的证据。",
                    "当前工单不需要证据核验。",
                    "工单已关闭。教程提示：不是每条工单都需要提交证据。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[2],
                    "T_D01_001",
                    "Stage_Day1",
                    "骑手被系统判定绕路，要求撤销罚款",
                    "李驰",
                    "旧港片区 4 栋 602",
                    "导航罚款",
                    "等待 00:24:16",
                    "我按系统路线走，最后还扣了 60 块。那段路明明封了，是系统让我绕行。",
                    "平台已根据服务规范自动完成罚款记录。如您对结果有疑问，可在 24 小时内提交申诉。",
                    "用户：李驰\n账户状态：活跃\n住户认证：有效",
                    "近 14 天提交过 4 次配送申诉；同一路段多次出现施工绕行。",
                    "建议维持系统判定，优先引导用户自行申诉。",
                    "订单 MW-FD-7721；导航路线经过已封闭道路；地图更新时间晚于施工时间。",
                    true,
                    "E_ROUTE_CONSTRUCTION",
                    "A07：平台路线与施工封路记录一致，绕行原因来自施工封路后的地图延迟。本次证据有效。",
                    "A07：后台未查询到能够支持该证据的相关记录，证据无效。",
                    "工单已关闭。用户认为系统没有真正核验路线。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[3],
                    "T_D01_002",
                    "Stage_Day1",
                    "AI 招聘系统误判简历造假，要求恢复面试资格",
                    "许闻笙",
                    "明湾新区 9 栋 1502",
                    "招聘审核",
                    "等待 00:18:32",
                    "系统说我的工作经历名称不一致，可那是公司后来改过名字。我能提供更名证明。",
                    "申请因经历匹配异常未进入下一轮，结果由系统综合评估生成，暂不支持人工复核。",
                    "用户：许闻笙\n账户状态：活跃\n住户认证：有效",
                    "历史工单显示旧公司与新公司使用统一社会信用代码，且招聘规则已更新。",
                    "建议维持自动结论，避免承诺恢复面试资格。",
                    "招聘初筛一致性评分 89/100；公司名称字段不完全一致；人工复核状态未触发。",
                    true,
                    "E_RECRUIT_HISTORY",
                    "A07：历史申请记录证明公司曾更名，当前异常属于名称匹配问题。本次证据有效。",
                    "A07：当前资料不足以证明系统误判，本次证据无效。",
                    "工单已关闭。用户认为异常标签没有得到人工核验。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[4],
                    "T_D01_003",
                    "Stage_Day1",
                    "健康腕带误报“离床正常”，家属要求核查",
                    "贺青岚",
                    "临江社区 2 栋 804",
                    "健康设备",
                    "等待 00:09:45",
                    "老人一直在家，腕带却显示离床正常。邻居敲门也没人应，我担心设备出了问题。",
                    "健康腕带数据显示用户已完成离床动作，暂无异常告警。建议稍后再次联系。",
                    "用户：贺青岚\n账户状态：活跃\n住户认证：有效",
                    "最近 24 小时低血糖提醒已发送；设备曾出现离线与缓存数据同步。",
                    "建议继续观察，不直接触发紧急人工通道。",
                    "设备电量 7%；设备断开连接；今日实时步数、心率与定位均无记录；缓存数据被误判为今日数据。",
                    true,
                    "E_HEALTH_CACHE",
                    "A07：设备日志确认腕带已断开连接，当前状态无法证明老人正常。本次证据有效。",
                    "A07：当前资料无法证明实时状态异常，本次证据无效。",
                    "工单已关闭。用户担心系统关闭了可能的紧急情况。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[5],
                    "T_D01_004",
                    "Stage_Day1",
                    "自媒体账号被 AI 判定违规搬运，要求恢复发布权限",
                    "梁雯",
                    "云桥公寓 6 栋 1103",
                    "内容审核",
                    "等待 00:37:08",
                    "我的账号突然被限制发布，说我搬运内容。那篇文章是我自己写的，采访也是我做的。",
                    "内容安全系统检测到重复内容和敏感特征，可能涉及低质搬运或 AI 生成内容。",
                    "用户：梁雯\n账户状态：活跃\n住户认证：有效",
                    "多次申诉均被自动归类为低影响力创作者，未进入人工复核。",
                    "建议维持风险标签，避免主动恢复发布权限。",
                    "内容发布时间早于相似内容；原始采访录音与平台上传时间一致；系统误把引用判定为搬运。",
                    true,
                    "E_CONTENT_SOURCE",
                    "A07：发布时间与原始采访记录证明该内容并非违规搬运。本次证据有效。",
                    "A07：当前资料不能排除搬运风险，本次证据无效。",
                    "工单已关闭。用户认为系统只是在重复风险结论。")
            };
        }

        private static TicketData CreateOrUpdateTicket(
            string assetPath,
            string ticketId,
            string stageId,
            string title,
            string userName,
            string region,
            string issueType,
            string waitTimeText,
            string userMessage,
            string aiReply,
            string profileText,
            string historyText,
            string deviceLogText,
            string regionStatusText,
            bool hasEvidence,
            string evidenceId,
            string onSaveEvidenceText,
            string onWrongEvidenceText,
            string onResolvedText)
        {
            TicketData ticket = AssetDatabase.LoadAssetAtPath<TicketData>(assetPath);
            if (ticket == null)
            {
                ticket = ScriptableObject.CreateInstance<TicketData>();
                AssetDatabase.CreateAsset(ticket, assetPath);
            }

            SerializedObject serializedTicket = new(ticket);
            SetString(serializedTicket, "ticketId", ticketId);
            SetString(serializedTicket, "stageId", stageId);
            SetString(serializedTicket, "title", title);
            SetString(serializedTicket, "userName", userName);
            SetString(serializedTicket, "region", region);
            SetString(serializedTicket, "issueType", issueType);
            SetString(serializedTicket, "waitTimeText", waitTimeText);
            SetString(serializedTicket, "userMessage", userMessage);
            SetString(serializedTicket, "aiReply", aiReply);
            SetString(serializedTicket, "profileText", profileText);
            SetString(serializedTicket, "historyText", historyText);
            SetString(serializedTicket, "deviceLogText", deviceLogText);
            SetString(serializedTicket, "regionStatusText", regionStatusText);
            serializedTicket.FindProperty("hasEvidence").boolValue = hasEvidence;
            SetString(serializedTicket, "evidenceId", evidenceId);
            SetString(serializedTicket, "onSaveEvidenceText", onSaveEvidenceText);
            SetString(serializedTicket, "onWrongEvidenceText", onWrongEvidenceText);
            SetString(serializedTicket, "onResolvedText", onResolvedText);
            ConfigureTicketFlow(serializedTicket, ticketId);
            serializedTicket.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ticket);
            return ticket;
        }

        private static void ConfigureTicketFlow(
            SerializedObject ticket,
            string ticketId)
        {
            SetString(
                ticket,
                "transferText",
                "AI《明湾通》：已转人工部门继续处理。\nA07：您好，《明湾通》A07 客服在线为您服务。");
            SetString(
                ticket,
                "evidencePromptText",
                "请从资料01～资料04中选择最能支持用户诉求的证据。");
            SetMetricDelta(ticket, "followUpMetricDelta", 0, 0, 0, 0);
            SetMetricDelta(ticket, "transferMetricDelta", 0, 1, 0, 0);

            switch (ticketId)
            {
                case "T_S01_001":
                    SetString(
                        ticket,
                        "followUpText",
                        "追问：同楼居民一周前也反馈过这个问题，转人工后就恢复了。");
                    ticket.FindProperty("requiresEvidenceSelection").boolValue = true;
                    ticket.FindProperty("correctEvidenceIndex").intValue = 3;
                    SetMetricDelta(ticket, "correctEvidenceMetricDelta", 1, 0, 0, 0);
                    SetMetricDelta(ticket, "wrongEvidenceMetricDelta", 1, 0, 0, 0);
                    SetMetricDelta(ticket, "resolvedMetricDelta", 1, 0, 0, 0);
                    break;

                case "T_S01_002":
                    SetString(
                        ticket,
                        "followUpText",
                        "追问：扣款前没有短信、邮件或弹窗提醒，我今天已经少吃一顿饭了。");
                    ticket.FindProperty("requiresEvidenceSelection").boolValue = false;
                    ticket.FindProperty("correctEvidenceIndex").intValue = 0;
                    SetMetricDelta(ticket, "correctEvidenceMetricDelta", 0, 0, 0, 0);
                    SetMetricDelta(ticket, "wrongEvidenceMetricDelta", 0, 0, 0, 0);
                    SetMetricDelta(ticket, "resolvedMetricDelta", 1, 0, 0, 0);
                    break;

                case "T_D01_001":
                    ConfigureFormalTicket(
                        ticket,
                        "追问：我申诉过三次，都是“证据不足”。一个骑手怎么证明施工封路？",
                        3,
                        18,
                        18,
                        -22,
                        5);
                    break;

                case "T_D01_002":
                    ConfigureFormalTicket(
                        ticket,
                        "追问：公司后来改过名字，我能提供更名证明和统一信用代码。",
                        1,
                        20,
                        -10,
                        -24,
                        6);
                    break;

                case "T_D01_003":
                    ConfigureFormalTicket(
                        ticket,
                        "追问：老人三个小时没人接电话，我要求人工核查或联系紧急联系人。",
                        3,
                        24,
                        -18,
                        -30,
                        8);
                    break;

                case "T_D01_004":
                    ConfigureFormalTicket(
                        ticket,
                        "追问：账号被封时我正在外地采访，原始录音和发布时间都能证明内容是原创。",
                        3,
                        22,
                        -14,
                        -30,
                        9);
                    break;
            }

        }

        private static void ConfigureFormalTicket(
            SerializedObject ticket,
            string followUpText,
            int correctEvidenceIndex,
            int correctSatisfaction,
            int wrongSatisfaction,
            int resolvedSatisfaction,
            int correctRisk)
        {
            SetString(ticket, "followUpText", followUpText);
            ticket.FindProperty("requiresEvidenceSelection").boolValue = true;
            ticket.FindProperty("correctEvidenceIndex").intValue = correctEvidenceIndex;
            SetMetricDelta(
                ticket,
                "correctEvidenceMetricDelta",
                1,
                0,
                correctSatisfaction,
                correctRisk);
            SetMetricDelta(
                ticket,
                "wrongEvidenceMetricDelta",
                1,
                0,
                wrongSatisfaction,
                3);
            SetMetricDelta(
                ticket,
                "resolvedMetricDelta",
                1,
                0,
                resolvedSatisfaction,
                0);
        }

        private static void SetMetricDelta(
            SerializedObject target,
            string propertyName,
            int resolvedCount,
            int transferCount,
            int userSatisfaction,
            int a07Risk)
        {
            SerializedProperty delta = target.FindProperty(propertyName);
            delta.FindPropertyRelative("resolvedCount").intValue = resolvedCount;
            delta.FindPropertyRelative("transferCount").intValue = transferCount;
            delta.FindPropertyRelative("userSatisfaction").intValue = userSatisfaction;
            delta.FindPropertyRelative("a07Risk").intValue = a07Risk;
        }

        private static void MigrateLegacyTicketAssets()
        {
            for (int index = 0;
                 index < LegacyTicketAssetPaths.Length &&
                 index < TicketAssetPaths.Length;
                 index++)
            {
                if (AssetDatabase.LoadMainAssetAtPath(TicketAssetPaths[index]) != null ||
                    AssetDatabase.LoadMainAssetAtPath(LegacyTicketAssetPaths[index]) == null)
                {
                    continue;
                }

                string error = AssetDatabase.MoveAsset(
                    LegacyTicketAssetPaths[index],
                    TicketAssetPaths[index]);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        $"迁移旧工单资产失败：{LegacyTicketAssetPaths[index]} → " +
                        $"{TicketAssetPaths[index]}\n{error}");
                }
            }
        }

        private static DemoDatabase CreateOrUpdateDatabase(TicketData[] ticketAssets)
        {
            DemoDatabase database = AssetDatabase.LoadAssetAtPath<DemoDatabase>(DatabaseAssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<DemoDatabase>();
                AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            }

            SerializedObject serializedDatabase = new(database);
            SerializedProperty tickets = serializedDatabase.FindProperty("tickets");
            tickets.arraySize = ticketAssets.Length;
            for (int index = 0; index < ticketAssets.Length; index++)
            {
                tickets.GetArrayElementAtIndex(index).objectReferenceValue = ticketAssets[index];
            }
            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            return database;
        }

        private static TMP_FontAsset EnsureChineseFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ChineseSourceFontPath);
            if (font == null || sourceFont == null)
            {
                throw new FileNotFoundException(
                    "缺少项目中文字体。请确认 NotoSansSC-Regular.ttf 与 " +
                    "NotoSansSC-Regular SDF.asset 均位于 Assets/UI/Fonts。");
            }

            return RebuildProjectChineseFont(font, sourceFont);
        }

        /// <summary>
        /// 将项目实际使用的字符烘焙到字体图集，并切换为静态模式。
        /// 静态字体不会在渲染时修改 .asset，可避免 NativeFormatImporter 结果不一致。
        /// </summary>
        [MenuItem("明湾/场景工具/应用 Noto Sans SC 中文字体")]
        public static void RepairChineseFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ChineseSourceFontPath);
            if (font == null || sourceFont == null)
            {
                throw new FileNotFoundException(
                    "缺少项目中文字体。请确认 NotoSansSC-Regular.ttf 与 " +
                    "NotoSansSC-Regular SDF.asset 均位于 Assets/UI/Fonts。");
            }

            font = RebuildProjectChineseFont(font, sourceFont);
            NormalizeProjectSceneFonts(font);
            Debug.Log("Noto Sans SC 中文字体已修复，场景文本已切换为截断模式。");
        }

        /// <summary>
        /// 使用项目内 Noto Sans SC 烘焙项目实际需要的字形。
        /// 烘焙完成后切换为静态字体，避免运行时修改 Atlas。
        /// </summary>
        private static TMP_FontAsset RebuildProjectChineseFont(
            TMP_FontAsset font,
            Font sourceFont)
        {
            SerializedObject serializedFont = new(font);
            serializedFont.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
            serializedFont.FindProperty("m_SourceFontFilePath").stringValue = string.Empty;
            serializedFont.FindProperty("m_SourceFontFileGUID").stringValue =
                AssetDatabase.AssetPathToGUID(ChineseSourceFontPath);
            serializedFont.FindProperty("m_AtlasPopulationMode").intValue =
                (int)AtlasPopulationMode.Dynamic;
            serializedFont.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
            serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();

            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = true;
            EnsureDynamicAtlasTexturesAreReadable(font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ChineseFontPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            if (font == null)
            {
                throw new InvalidOperationException("重新导入 Noto Sans SC 字体资产失败。");
            }

            font.ClearFontAssetData();
            bool addedAllCharacters = font.TryAddCharacters(
                BuildProjectCharacterSet(),
                out string missingCharacters,
                true);

            foreach (Texture2D atlasTexture in font.atlasTextures)
            {
                if (atlasTexture != null)
                {
                    AttachObjectToFontAssetIfNeeded(atlasTexture, font);
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.ReadFontAssetDefinition();
            serializedFont = new SerializedObject(font);
            serializedFont.FindProperty("m_AtlasPopulationMode").intValue =
                (int)AtlasPopulationMode.Static;
            serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();

            if (font.material != null && font.atlasTextures.Length > 0)
            {
                font.material.SetTexture(
                    ShaderUtilities.ID_MainTex,
                    font.atlasTextures[0]);
                EditorUtility.SetDirty(font.material);
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();

            if (!addedAllCharacters && !string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning(
                    $"Noto Sans SC 缺少以下字符，将使用方框显示：{missingCharacters}",
                    font);
            }

            return font;
        }

        /// <summary>
        /// Dynamic TMP 字体必须能向 Atlas 写入新字形。
        /// Font Asset Creator 生成的静态 Atlas 默认不可读，直接改成 Dynamic 会导致中文写入失败。
        /// </summary>
        private static void EnsureDynamicAtlasTexturesAreReadable(TMP_FontAsset font)
        {
            if (font.atlasTextures == null || font.atlasTextures.Length == 0)
            {
                throw new InvalidOperationException(
                    $"字体“{font.name}”没有有效的 Atlas Texture，请重新生成字体资产。");
            }

            foreach (Texture2D atlasTexture in font.atlasTextures)
            {
                if (atlasTexture == null)
                {
                    throw new InvalidOperationException(
                        $"字体“{font.name}”包含丢失的 Atlas Texture 引用。");
                }

                SerializedObject serializedTexture = new(atlasTexture);
                SerializedProperty readableProperty =
                    serializedTexture.FindProperty("m_IsReadable");
                if (readableProperty != null && !readableProperty.boolValue)
                {
                    readableProperty.boolValue = true;
                    serializedTexture.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            if (font.material == null)
            {
                throw new InvalidOperationException(
                    $"字体“{font.name}”没有有效的 TMP 材质。");
            }

            font.material.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTextures[0]);
            EditorUtility.SetDirty(font.material);
        }

        /// <summary>
        /// 将 TMP 生成的图集或材质挂到字体资产下。
        /// 多图集生成后 Unity 可能已经把对象写入 .asset，但 IsSubAsset 尚未及时刷新，
        /// 因此使用实际资产路径判断，保证重复烘焙时不会再次 AddObjectToAsset。
        /// </summary>
        private static void AttachObjectToFontAssetIfNeeded(
            UnityEngine.Object childAsset,
            TMP_FontAsset font)
        {
            if (childAsset == null)
            {
                return;
            }

            string existingAssetPath =
                AssetDatabase.GetAssetPath(childAsset).Replace('\\', '/');
            if (existingAssetPath == ChineseFontPath)
            {
                return;
            }

            if (!string.IsNullOrEmpty(existingAssetPath))
            {
                throw new InvalidOperationException(
                    $"字体子资源“{childAsset.name}”已经属于其他资产：{existingAssetPath}");
            }

            AssetDatabase.AddObjectToAsset(childAsset, font);
        }

        private static string BuildProjectCharacterSet()
        {
            HashSet<char> characters = new();
            for (char character = ' '; character <= '~'; character++)
            {
                characters.Add(character);
            }

            // TMP 的 Ellipsis 模式固定查找 U+2026；即使当前场景改用截断，也保留该常用符号。
            characters.Add('\u2026');

            string[] textExtensions =
            {
                ".cs", ".asset", ".unity", ".json", ".md", ".txt"
            };

            foreach (string path in Directory.GetFiles(
                         "Assets",
                         "*.*",
                         SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/') == ChineseFontPath ||
                    Array.IndexOf(textExtensions, Path.GetExtension(path).ToLowerInvariant()) < 0)
                {
                    continue;
                }

                string content;
                try
                {
                    content = File.ReadAllText(path);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (char character in content)
                {
                    if (ShouldBakeCharacter(character))
                    {
                        characters.Add(character);
                    }
                }
            }

            List<char> orderedCharacters = new(characters);
            orderedCharacters.Sort();
            StringBuilder builder = new(orderedCharacters.Count);
            foreach (char character in orderedCharacters)
            {
                builder.Append(character);
            }

            return builder.ToString();
        }

        private static void NormalizeProjectSceneFonts(TMP_FontAsset replacementFont)
        {
            NormalizeSceneFont(GameScenePath, replacementFont);
            NormalizeSceneFont(TitleScenePath, replacementFont);
        }

        private static void NormalizeSceneFont(
            string scenePath,
            TMP_FontAsset replacementFont)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForMigration = !scene.IsValid() || !scene.isLoaded;

            if (openedForMigration)
            {
                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
            }

            bool changed = false;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (TMP_Text text in rootObject.GetComponentsInChildren<TMP_Text>(true))
                {
                    bool textChanged = false;
                    if (text.font != replacementFont)
                    {
                        text.font = replacementFont;
                        text.fontSharedMaterial = replacementFont.material;
                        textChanged = true;
                    }

                    if (text.overflowMode != TextOverflowModes.Truncate)
                    {
                        text.overflowMode = TextOverflowModes.Truncate;
                        textChanged = true;
                    }

                    if (textChanged)
                    {
                        EditorUtility.SetDirty(text);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (openedForMigration)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool ShouldBakeCharacter(char character)
        {
            return character is >= ' ' and <= '~' ||
                   character is >= '\u00A0' and <= '\u024F' ||
                   character is >= '\u2000' and <= '\u206F' ||
                   character is >= '\u3000' and <= '\u303F' ||
                   character is >= '\u3400' and <= '\u4DBF' ||
                   character is >= '\u4E00' and <= '\u9FFF' ||
                   character is >= '\uFF00' and <= '\uFFEF';
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;
            return camera;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void CreateTopBar(
            RectTransform root,
            TMP_FontAsset font,
            out TMP_Text progressText,
            out TMP_Text statusText,
            out TMP_Text evidenceText,
            out TMP_Text resolvedText)
        {
            RectTransform topBar = CreateImage(
                "TopBar",
                root,
                TopBar,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, 82f),
                new Vector2(0f, -41f));

            CreateImage(
                "AccentLine",
                topBar,
                Accent,
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(0f, 2f),
                Vector2.zero);

            progressText = CreateText(
                "Txt_TicketProgress",
                topBar,
                font,
                "工单 1 / 1  ·  T_001",
                22f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                new Vector2(0.3f, 1f),
                new Vector2(36f, 0f),
                Vector2.zero);

            statusText = CreateText(
                "Txt_Status",
                topBar,
                font,
                "状态：等待核验",
                19f,
                FontStyles.Normal,
                Accent,
                TextAlignmentOptions.Center,
                new Vector2(0.3f, 0f),
                new Vector2(0.68f, 1f),
                Vector2.zero,
                Vector2.zero);

            evidenceText = CreateText(
                "Txt_EvidenceCount",
                topBar,
                font,
                "证据  0",
                18f,
                FontStyles.Bold,
                Warning,
                TextAlignmentOptions.MidlineRight,
                new Vector2(0.68f, 0f),
                new Vector2(0.84f, 1f),
                Vector2.zero,
                new Vector2(-18f, 0f));

            resolvedText = CreateText(
                "Txt_ResolvedCount",
                topBar,
                font,
                "已解决  0",
                18f,
                FontStyles.Bold,
                MutedText,
                TextAlignmentOptions.MidlineRight,
                new Vector2(0.84f, 0f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-36f, 0f));
        }

        private static void CreateQueuePanel(
            RectTransform root,
            TMP_FontAsset font,
            out RectTransform queueContent,
            out TicketQueueItemView queueItemTemplate)
        {
            RectTransform panel = CreatePanel(
                "TicketQueuePanel",
                root,
                new Vector2(0.02f, 0.1f),
                new Vector2(0.18f, 0.9f));

            CreateText(
                "Txt_QueueTitle",
                panel,
                font,
                "工单队列",
                25f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.88f),
                Vector2.one,
                new Vector2(24f, 0f),
                new Vector2(-18f, 0f));

            queueContent = CreateRect(
                "QueueContent",
                panel,
                new Vector2(0.05f, 0.16f),
                new Vector2(0.95f, 0.84f),
                Vector2.zero,
                Vector2.zero);
            queueContent.pivot = new Vector2(0.5f, 1f);

            RectTransform queueItem = CreateImage(
                "QueueItemTemplate",
                queueContent,
                PanelRaised,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, 90f),
                Vector2.zero);
            queueItem.pivot = new Vector2(0.5f, 1f);
            Button queueButton = queueItem.gameObject.AddComponent<Button>();
            ColorBlock colors = queueButton.colors;
            colors.normalColor = PanelRaised;
            colors.highlightedColor = AccentHover;
            colors.pressedColor = Accent;
            colors.selectedColor = AccentHover;
            colors.disabledColor = PanelDark;
            queueButton.colors = colors;

            CreateImage(
                "TicketIconPlaceholder",
                queueItem,
                Accent,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(42f, 42f),
                new Vector2(34f, 0f));

            TMP_Text summaryText = CreateText(
                "Txt_QueueSummary",
                queueItem,
                font,
                "T_001\n工单类型",
                17f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.24f, 0f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-12f, 0f));

            queueItemTemplate = queueItem.gameObject.AddComponent<TicketQueueItemView>();
            SerializedObject serializedItem = new(queueItemTemplate);
            SetObject(serializedItem, "button", queueButton);
            SetObject(serializedItem, "backgroundImage", queueItem.GetComponent<Image>());
            SetObject(serializedItem, "summaryText", summaryText);
            serializedItem.FindProperty("normalColor").colorValue = PanelRaised;
            serializedItem.FindProperty("selectedColor").colorValue = Accent;
            serializedItem.FindProperty("processedColor").colorValue = PanelDark;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            queueItem.gameObject.SetActive(false);

            CreateText(
                "Txt_QueueHint",
                panel,
                font,
                "点击待处理工单查看内容。\n已处理工单不可重复操作。",
                16f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.05f),
                new Vector2(1f, 0.62f),
                new Vector2(24f, 0f),
                new Vector2(-24f, 0f));
        }

        private static void CreateTicketPanel(
            RectTransform root,
            TMP_FontAsset font,
            out TMP_Text titleText,
            out TMP_Text metaText,
            out TMP_Text userMessageText,
            out TMP_Text aiReplyText)
        {
            RectTransform panel = CreatePanel(
                "TicketPanel",
                root,
                new Vector2(0.195f, 0.28f),
                new Vector2(0.645f, 0.9f));

            titleText = CreateText(
                "Txt_TicketTitle",
                panel,
                font,
                "门禁故障：整层居民无法进入",
                30f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.84f),
                new Vector2(0.72f, 1f),
                new Vector2(30f, 0f),
                Vector2.zero);

            metaText = CreateText(
                "Txt_TicketMeta",
                panel,
                font,
                "用户：林女士\n区域：明湾旧区 · 7号楼\n类型：门禁异常\n等待 01:42:18",
                16f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.TopRight,
                new Vector2(0.7f, 0.82f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-28f, -16f));

            CreateImage(
                "HeaderDivider",
                panel,
                Border,
                new Vector2(0f, 0.8f),
                new Vector2(1f, 0.8f),
                new Vector2(-60f, 1f),
                Vector2.zero);

            RectTransform userBubble = CreateImage(
                "UserMessagePanel",
                panel,
                PanelRaised,
                new Vector2(0.04f, 0.47f),
                new Vector2(0.9f, 0.75f));

            CreateImage(
                "UserAvatarPlaceholder",
                userBubble,
                Warning,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(54f, 54f),
                new Vector2(34f, -34f));

            CreateText(
                "Txt_UserLabel",
                userBubble,
                font,
                "居民求助",
                16f,
                FontStyles.Bold,
                Warning,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.12f, 0.7f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);

            userMessageText = CreateText(
                "Txt_UserMessage",
                userBubble,
                font,
                "我们整层楼从昨晚开始都打不开门禁，不是我一个人的手机问题。",
                20f,
                FontStyles.Normal,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0.12f, 0.05f),
                new Vector2(0.96f, 0.72f),
                Vector2.zero,
                Vector2.zero);

            RectTransform aiBubble = CreateImage(
                "AiMessagePanel",
                panel,
                PanelDark,
                new Vector2(0.1f, 0.12f),
                new Vector2(0.96f, 0.4f));

            CreateImage(
                "AiAvatarPlaceholder",
                aiBubble,
                Accent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(54f, 54f),
                new Vector2(34f, -34f));

            CreateText(
                "Txt_AiLabel",
                aiBubble,
                font,
                "明湾通 AI",
                16f,
                FontStyles.Bold,
                Accent,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.12f, 0.7f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);

            aiReplyText = CreateText(
                "Txt_AiReply",
                aiBubble,
                font,
                "检测结果显示门禁服务运行正常。请确认手机蓝牙权限已开启。",
                20f,
                FontStyles.Normal,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0.12f, 0.05f),
                new Vector2(0.96f, 0.72f),
                Vector2.zero,
                Vector2.zero);
        }

        private static void CreateDataArea(
            RectTransform root,
            TMP_FontAsset font,
            out GameObject dataPanel,
            out TMP_Text profileText,
            out TMP_Text historyText,
            out TMP_Text deviceLogText,
            out TMP_Text regionStatusText)
        {
            RectTransform lockedPanel = CreatePanel(
                "DataLockedPlaceholder",
                root,
                new Vector2(0.66f, 0.28f),
                new Vector2(0.98f, 0.9f));

            CreateImage(
                "DataIconPlaceholder",
                lockedPanel,
                MutedText,
                new Vector2(0.5f, 0.62f),
                new Vector2(0.5f, 0.62f),
                new Vector2(92f, 92f),
                Vector2.zero);

            CreateText(
                "Txt_DataLocked",
                lockedPanel,
                font,
                "资料尚未展开",
                28f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0.42f),
                new Vector2(1f, 0.57f),
                Vector2.zero,
                Vector2.zero);

            CreateText(
                "Txt_DataLockedHint",
                lockedPanel,
                font,
                "查看用户资料、历史工单、设备日志与区域状态，\n核验 AI 回复是否可信。",
                17f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.Top,
                new Vector2(0.08f, 0.23f),
                new Vector2(0.92f, 0.42f),
                Vector2.zero,
                Vector2.zero);

            RectTransform dataRect = CreatePanel(
                "DataPanel",
                root,
                new Vector2(0.66f, 0.28f),
                new Vector2(0.98f, 0.9f));
            dataPanel = dataRect.gameObject;

            CreateText(
                "Txt_DataTitle",
                dataRect,
                font,
                "核验资料",
                26f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.9f),
                Vector2.one,
                new Vector2(24f, 0f),
                new Vector2(-20f, 0f));

            profileText = CreateDataCard(dataRect, font, "资料01 用户资料", new Vector2(0.04f, 0.69f), new Vector2(0.96f, 0.88f));
            historyText = CreateDataCard(dataRect, font, "资料02 历史工单", new Vector2(0.04f, 0.47f), new Vector2(0.96f, 0.66f));
            deviceLogText = CreateDataCard(dataRect, font, "资料03 AI建议", new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.44f));
            regionStatusText = CreateDataCard(dataRect, font, "资料04 系统日志", new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.22f));

            dataPanel.SetActive(false);
        }

        private static TMP_Text CreateDataCard(
            RectTransform parent,
            TMP_FontAsset font,
            string title,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            RectTransform card = CreateImage(title + "Card", parent, PanelRaised, anchorMin, anchorMax);

            CreateImage(
                title + "IconPlaceholder",
                card,
                Accent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, 28f),
                new Vector2(22f, -22f));

            CreateText(
                "Txt_" + title + "Title",
                card,
                font,
                title,
                16f,
                FontStyles.Bold,
                Accent,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.12f, 0.67f),
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return CreateText(
                "Txt_" + title,
                card,
                font,
                "占位资料",
                14f,
                FontStyles.Normal,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0.05f, 0.04f),
                new Vector2(0.95f, 0.68f),
                Vector2.zero,
                Vector2.zero);
        }

        private static void CreateActionBar(
            RectTransform root,
            TMP_FontAsset font,
            out Button primaryActionButton,
            out Button transferHumanButton,
            out Button markResolvedButton)
        {
            RectTransform bar = CreatePanel(
                "ActionBar",
                root,
                new Vector2(0.195f, 0.08f),
                new Vector2(0.98f, 0.24f));

            CreateText(
                "Txt_ActionHint",
                bar,
                font,
                "先核验资料，再选择追问、转人工或标记已解决。",
                16f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.02f, 0.65f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero);

            primaryActionButton = CreateButton(
                "Btn_ViewData",
                bar,
                font,
                "查看资料",
                Accent,
                AccentHover,
                Background,
                new Vector2(0.19f, 0.34f),
                new Vector2(300f, 62f));

            transferHumanButton = CreateButton(
                "Btn_SaveEvidence",
                bar,
                font,
                "转人工",
                Warning,
                Hex("B0B0B0"),
                Background,
                new Vector2(0.53f, 0.34f),
                new Vector2(300f, 62f));

            markResolvedButton = CreateButton(
                "Btn_MarkResolved",
                bar,
                font,
                "标记已解决",
                PanelRaised,
                Hex("5A5A5A"),
                PrimaryText,
                new Vector2(0.83f, 0.34f),
                new Vector2(300f, 62f));
        }

        private static void CreateResultPanel(
            RectTransform root,
            TMP_FontAsset font,
            out GameObject resultPanel,
            out TMP_Text resultTitleText,
            out TMP_Text resultDescriptionText,
            out TMP_Text resultMetricsText,
            out Button resultActionButton)
        {
            RectTransform overlay = CreateImage(
                "ResultPanel",
                root,
                new Color(0.08f, 0.08f, 0.08f, 0.94f),
                Vector2.zero,
                Vector2.one);
            resultPanel = overlay.gameObject;

            RectTransform card = CreatePanel(
                "ResultCard",
                overlay,
                new Vector2(0.33f, 0.23f),
                new Vector2(0.67f, 0.77f));
            card.gameObject.GetComponent<Image>().color = Panel;

            CreateImage(
                "ResultIconPlaceholder",
                card,
                Accent,
                new Vector2(0.5f, 0.8f),
                new Vector2(0.5f, 0.8f),
                new Vector2(74f, 74f),
                Vector2.zero);

            resultTitleText = CreateText(
                "Txt_ResultTitle",
                card,
                font,
                "处理完成",
                34f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.61f),
                new Vector2(0.95f, 0.75f),
                Vector2.zero,
                Vector2.zero);

            resultDescriptionText = CreateText(
                "Txt_ResultDescription",
                card,
                font,
                "结果说明占位",
                19f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.Top,
                new Vector2(0.09f, 0.37f),
                new Vector2(0.91f, 0.6f),
                Vector2.zero,
                Vector2.zero);

            resultMetricsText = CreateText(
                "Txt_ResultMetrics",
                card,
                font,
                "证据数量：0\n已解决数量：0",
                17f,
                FontStyles.Bold,
                Warning,
                TextAlignmentOptions.Center,
                new Vector2(0.15f, 0.21f),
                new Vector2(0.85f, 0.37f),
                Vector2.zero,
                Vector2.zero);

            resultActionButton = CreateButton(
                "Btn_ResultAction",
                card,
                font,
                "返回工单列表",
                Accent,
                AccentHover,
                Background,
                new Vector2(0.5f, 0.12f),
                new Vector2(320f, 64f));
            resultPanel.SetActive(false);
        }

        private static void BindView(
            MainGameView view,
            RectTransform queueContent,
            TicketQueueItemView queueItemTemplate,
            GameObject[] ticketContentObjects,
            TMP_Text progressText,
            TMP_Text statusText,
            TMP_Text evidenceText,
            TMP_Text resolvedText,
            TMP_Text titleText,
            TMP_Text metaText,
            TMP_Text userMessageText,
            TMP_Text aiReplyText,
            GameObject dataPanel,
            TMP_Text profileText,
            TMP_Text historyText,
            TMP_Text deviceLogText,
            TMP_Text regionStatusText,
            Button primaryActionButton,
            Button transferHumanButton,
            Button markResolvedButton,
            GameObject resultPanel,
            TMP_Text resultTitleText,
            TMP_Text resultDescriptionText,
            TMP_Text resultMetricsText,
            Button resultActionButton)
        {
            SerializedObject serializedView = new(view);
            SetObject(serializedView, "ticketQueueContent", queueContent);
            SetObject(serializedView, "ticketQueueItemTemplate", queueItemTemplate);
            SerializedProperty contentObjects = serializedView.FindProperty("ticketContentObjects");
            contentObjects.arraySize = ticketContentObjects.Length;
            for (int index = 0; index < ticketContentObjects.Length; index++)
            {
                contentObjects.GetArrayElementAtIndex(index).objectReferenceValue =
                    ticketContentObjects[index];
            }
            SetObject(serializedView, "ticketProgressText", progressText);
            SetObject(serializedView, "statusText", statusText);
            SetObject(serializedView, "evidenceCountText", evidenceText);
            SetObject(serializedView, "resolvedCountText", resolvedText);
            SetObject(serializedView, "ticketTitleText", titleText);
            SetObject(serializedView, "ticketMetaText", metaText);
            SetObject(serializedView, "userMessageText", userMessageText);
            SetObject(serializedView, "aiReplyText", aiReplyText);
            SetObject(serializedView, "dataPanel", dataPanel);
            SetObject(serializedView, "profileText", profileText);
            SetObject(serializedView, "historyText", historyText);
            SetObject(serializedView, "deviceLogText", deviceLogText);
            SetObject(serializedView, "regionStatusText", regionStatusText);
            SetObject(serializedView, "primaryActionButton", primaryActionButton);
            SetObject(serializedView, "transferHumanButton", transferHumanButton);
            SetObject(serializedView, "markResolvedButton", markResolvedButton);
            SetObject(serializedView, "resultPanel", resultPanel);
            SetObject(serializedView, "resultTitleText", resultTitleText);
            SetObject(serializedView, "resultDescriptionText", resultDescriptionText);
            SetObject(serializedView, "resultMetricsText", resultMetricsText);
            SetObject(serializedView, "resultActionButton", resultActionButton);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindFlowManager(
            GameFlowManager flowManager,
            DemoDatabase database,
            EvidenceManager evidenceManager,
            MetricManager metricManager,
            MainGameView view)
        {
            SerializedObject serializedFlow = new(flowManager);
            SetObject(serializedFlow, "database", database);
            SetObject(serializedFlow, "evidenceManager", evidenceManager);
            SetObject(serializedFlow, "metricManager", metricManager);
            SetObject(serializedFlow, "mainGameView", view);
            serializedFlow.FindProperty("titleSceneName").stringValue = "TitleScene";
            serializedFlow.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform CreatePanel(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            RectTransform rect = CreateImage(name, parent, Panel, anchorMin, anchorMax);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            Color normalColor,
            Color highlightedColor,
            Color textColor,
            Vector2 anchor,
            Vector2 size)
        {
            RectTransform rect = CreateImage(name, parent, normalColor, anchor, anchor, size, Vector2.zero);
            Button button = rect.gameObject.AddComponent<Button>();

            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = Color.Lerp(highlightedColor, Background, 0.25f);
            colors.selectedColor = highlightedColor;
            colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.28f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            CreateText(
                "Label",
                rect,
                font,
                label,
                22f,
                FontStyles.Bold,
                textColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        private static RectTransform CreateImage(
            string name,
            RectTransform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2? sizeDelta = null,
            Vector2? anchoredPosition = null)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition ?? Vector2.zero,
                sizeDelta ?? Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string content,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
        }

        private static void CapturePreview(Canvas canvas, Camera camera)
        {
            const int width = 1920;
            const int height = 1080;

            RenderMode originalRenderMode = canvas.renderMode;
            Camera originalWorldCamera = canvas.worldCamera;
            RenderTexture originalTarget = camera.targetTexture;
            RenderTexture originalActive = RenderTexture.active;

            RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D preview = new(width, height, TextureFormat.RGB24, false);

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = renderTexture;

            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = renderTexture;
            preview.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            preview.Apply();

            string previewPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Logs", "GameScenePreview.png"));
            File.WriteAllBytes(previewPath, preview.EncodeToPNG());

            canvas.renderMode = originalRenderMode;
            canvas.worldCamera = originalWorldCamera;
            camera.targetTexture = originalTarget;
            RenderTexture.active = originalActive;

            UnityEngine.Object.DestroyImmediate(preview);
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void SetObject(
            SerializedObject target,
            string propertyName,
            UnityEngine.Object value)
        {
            target.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void SetString(SerializedObject target, string propertyName, string value)
        {
            target.FindProperty(propertyName).stringValue = value;
        }

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString($"#{value}", out Color color))
            {
                throw new System.ArgumentException($"无效颜色值：{value}");
            }

            return color;
        }
    }
}
