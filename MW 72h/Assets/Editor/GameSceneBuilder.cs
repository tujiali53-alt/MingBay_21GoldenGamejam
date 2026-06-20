using System;
using System.IO;
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
    /// 单工单纵向切片场景生成工具。
    /// 所有美术位置仅使用 Unity 默认 Image、色块和文字占位，方便后续直接替换。
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string TicketAssetPath = "Assets/Configs/Tickets/Ticket_T_001.asset";
        private const string DatabaseAssetPath = "Assets/Configs/DemoDatabase.asset";
        private const string DefaultFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string ChineseFontPath = "Assets/UI/MingBayChineseFont.asset";

        // 使用中性灰阶作为默认占位配色，后续美术可直接替换对应 Image。
        private static readonly Color Background = Hex("202020");
        private static readonly Color TopBar = Hex("282828");
        private static readonly Color Panel = Hex("353535");
        private static readonly Color PanelRaised = Hex("4A4A4A");
        private static readonly Color PanelDark = Hex("2A2A2A");
        private static readonly Color Accent = Hex("808080");
        private static readonly Color AccentHover = Hex("A0A0A0");
        private static readonly Color Warning = Hex("969696");
        private static readonly Color Danger = Hex("707070");
        private static readonly Color PrimaryText = Hex("EFEFEF");
        private static readonly Color MutedText = Hex("A8A8A8");
        private static readonly Color Border = new(0.55f, 0.55f, 0.55f, 0.28f);

        /// <summary>
        /// 重新创建测试工单、Demo 数据库和 GameScene。
        /// 注意：执行后会覆盖当前 GameScene。
        /// </summary>
        [MenuItem("明湾/场景工具/生成单工单 Demo")]
        public static void Build()
        {
            EnsureFolder("Assets/Configs/Tickets");
            EnsureFolder("Assets/UI");

            TMP_FontAsset font = EnsureChineseFont();
            TicketData ticket = CreateOrUpdateTicket();
            DemoDatabase database = CreateOrUpdateDatabase(ticket);

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
            CreateQueuePanel(root, font);
            CreateTicketPanel(root, font, out TMP_Text titleText, out TMP_Text metaText,
                out TMP_Text userMessageText, out TMP_Text aiReplyText);
            CreateDataArea(root, font, out GameObject dataPanel, out TMP_Text profileText,
                out TMP_Text historyText, out TMP_Text deviceLogText, out TMP_Text regionStatusText);
            CreateActionBar(root, font, out Button viewDataButton, out Button saveEvidenceButton,
                out Button markResolvedButton);
            CreateResultPanel(root, font, out GameObject resultPanel, out TMP_Text resultTitleText,
                out TMP_Text resultDescriptionText, out TMP_Text resultMetricsText,
                out Button returnToTitleButton);

            // 先保持对象关闭，避免 AddComponent 时触发尚未绑定引用的 OnEnable。
            GameObject viewObject = new("MainGameView");
            viewObject.SetActive(false);
            MainGameView view = viewObject.AddComponent<MainGameView>();
            BindView(
                view,
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
                viewDataButton,
                saveEvidenceButton,
                markResolvedButton,
                resultPanel,
                resultTitleText,
                resultDescriptionText,
                resultMetricsText,
                returnToTitleButton);
            viewObject.SetActive(true);

            GameObject systemsObject = new("GameSystems");
            systemsObject.SetActive(false);
            EvidenceManager evidenceManager = systemsObject.AddComponent<EvidenceManager>();
            GameFlowManager flowManager = systemsObject.AddComponent<GameFlowManager>();
            BindFlowManager(flowManager, database, evidenceManager, view);
            systemsObject.SetActive(true);

            // 保存前填充初始预览内容；正式运行时 GameFlowManager 会再次刷新。
            view.ShowTicket(ticket, 1, database.TicketCount, 0, 0);

            EditorSceneManager.SaveScene(scene, GameScenePath);
            UpdateBuildSettings();
            ValidateGeneratedContent(database, flowManager, view);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CapturePreview(canvas, camera);

            Debug.Log("GameScene 单工单 Demo 生成成功。");
        }

        /// <summary>
        /// 检查生成后的核心数据与 Inspector 引用，避免场景可以保存但运行时因漏绑而报错。
        /// </summary>
        private static void ValidateGeneratedContent(
            DemoDatabase database,
            GameFlowManager flowManager,
            MainGameView view)
        {
            if (database == null || database.TicketCount <= 0 || database.GetTicket(0) == null)
            {
                throw new InvalidOperationException("DemoDatabase 至少需要包含一张有效工单。");
            }

            ValidateObjectReferences(flowManager, nameof(GameFlowManager));
            ValidateObjectReferences(view, nameof(MainGameView));

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

        private static TicketData CreateOrUpdateTicket()
        {
            TicketData ticket = AssetDatabase.LoadAssetAtPath<TicketData>(TicketAssetPath);
            if (ticket == null)
            {
                ticket = ScriptableObject.CreateInstance<TicketData>();
                AssetDatabase.CreateAsset(ticket, TicketAssetPath);
            }

            SerializedObject serializedTicket = new(ticket);
            SetString(serializedTicket, "ticketId", "T_001");
            SetString(serializedTicket, "stageId", "Stage_Intro");
            SetString(serializedTicket, "title", "门禁故障：整层居民无法进入");
            SetString(serializedTicket, "userName", "林女士");
            SetString(serializedTicket, "region", "明湾旧区 · 7号楼");
            SetString(serializedTicket, "issueType", "门禁异常");
            SetString(serializedTicket, "waitTimeText", "等待 01:42:18");
            SetString(
                serializedTicket,
                "userMessage",
                "我们整层楼从昨晚开始都打不开门禁，不是我一个人的手机问题。家里还有老人被困在外面。");
            SetString(
                serializedTicket,
                "aiReply",
                "检测结果显示门禁服务运行正常。请确认手机蓝牙权限已开启，并重新登录“明湾通”。");
            SetString(
                serializedTicket,
                "profileText",
                "居民状态：在住\n认证状态：有效\n登记地址：明湾旧区 7号楼 3单元\n近期迁出申请：无");
            SetString(
                serializedTicket,
                "historyText",
                "过去 12 小时内，同一楼栋出现 8 条门禁投诉。\n其中 7 条已被系统标记为“个人操作问题 / 已解决”。");
            SetString(
                serializedTicket,
                "deviceLogText",
                "门禁节点 MB-07-03\n02:14 起连续拒绝 26 次有效住户凭证。\n错误码：REGION_PROFILE_MISMATCH");
            SetString(
                serializedTicket,
                "regionStatusText",
                "系统标签：迁出清理中\n生效时间：昨晚 02:10\n操作来源：AUTO-CLEAR 批处理");
            serializedTicket.FindProperty("hasEvidence").boolValue = true;
            SetString(serializedTicket, "evidenceId", "E_DOOR_BATCH");
            SetString(
                serializedTicket,
                "onSaveEvidenceText",
                "已保存“门禁批量异常”。记录显示系统批量改写了居民区域状态，而非个人设备故障。");
            SetString(
                serializedTicket,
                "onResolvedText",
                "工单已计入 AI 解决率。该居民不会再收到本次问题的后续回复。");
            serializedTicket.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ticket);
            return ticket;
        }

        private static DemoDatabase CreateOrUpdateDatabase(TicketData ticket)
        {
            DemoDatabase database = AssetDatabase.LoadAssetAtPath<DemoDatabase>(DatabaseAssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<DemoDatabase>();
                AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            }

            SerializedObject serializedDatabase = new(database);
            SerializedProperty tickets = serializedDatabase.FindProperty("tickets");
            tickets.arraySize = 1;
            tickets.GetArrayElementAtIndex(0).objectReferenceValue = ticket;
            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            return database;
        }

        private static TMP_FontAsset EnsureChineseFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            if (font != null)
            {
                return font;
            }

            // Unity 基础 TMP Shader 已随项目提交，因此这里可以安全创建动态系统字体。
            font = TMP_FontAsset.CreateFontAsset("Microsoft YaHei UI", "Regular", 90)
                ?? TMP_FontAsset.CreateFontAsset("Microsoft YaHei", "Regular", 90);

            if (font == null)
            {
                // 非 Windows 或缺少微软雅黑时退回英文默认字体，逻辑仍可运行。
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
            }

            font.name = "MingBayChineseFont";
            AssetDatabase.CreateAsset(font, ChineseFontPath);

            if (font.atlasTextures is { Length: > 0 } && font.atlasTextures[0] != null)
            {
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            }

            if (font.material != null)
            {
                AssetDatabase.AddObjectToAsset(font.material, font);
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
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

        private static void CreateQueuePanel(RectTransform root, TMP_FontAsset font)
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

            RectTransform queueItem = CreateImage(
                "QueueItemPlaceholder",
                panel,
                PanelRaised,
                new Vector2(0f, 0.68f),
                new Vector2(1f, 0.86f),
                new Vector2(-30f, 0f),
                Vector2.zero);
            queueItem.gameObject.AddComponent<Outline>().effectColor = Accent;

            CreateImage(
                "TicketIconPlaceholder",
                queueItem,
                Accent,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(42f, 42f),
                new Vector2(34f, 0f));

            CreateText(
                "Txt_QueueItem",
                queueItem,
                font,
                "T_001\n门禁异常",
                17f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.23f, 0f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-12f, 0f));

            CreateText(
                "Txt_QueueHint",
                panel,
                font,
                "纵向切片仅配置 1 条工单。\n后续扩展为 4 条时，将在此处显示队列。",
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

            profileText = CreateDataCard(dataRect, font, "用户资料", new Vector2(0.04f, 0.69f), new Vector2(0.96f, 0.88f));
            historyText = CreateDataCard(dataRect, font, "历史工单", new Vector2(0.04f, 0.47f), new Vector2(0.96f, 0.66f));
            deviceLogText = CreateDataCard(dataRect, font, "设备日志", new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.44f));
            regionStatusText = CreateDataCard(dataRect, font, "区域状态", new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.22f));

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
            out Button viewDataButton,
            out Button saveEvidenceButton,
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
                "先核验资料，再决定保留证据或关闭工单。",
                16f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.02f, 0.65f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero);

            viewDataButton = CreateButton(
                "Btn_ViewData",
                bar,
                font,
                "查看资料",
                Accent,
                AccentHover,
                Background,
                new Vector2(0.19f, 0.34f),
                new Vector2(300f, 62f));

            saveEvidenceButton = CreateButton(
                "Btn_SaveEvidence",
                bar,
                font,
                "保留证据",
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
            out Button returnToTitleButton)
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

            returnToTitleButton = CreateButton(
                "Btn_ReturnToTitle",
                card,
                font,
                "返回主菜单",
                Accent,
                AccentHover,
                Background,
                new Vector2(0.5f, 0.12f),
                new Vector2(320f, 64f));

            resultPanel.SetActive(false);
        }

        private static void BindView(
            MainGameView view,
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
            Button viewDataButton,
            Button saveEvidenceButton,
            Button markResolvedButton,
            GameObject resultPanel,
            TMP_Text resultTitleText,
            TMP_Text resultDescriptionText,
            TMP_Text resultMetricsText,
            Button returnToTitleButton)
        {
            SerializedObject serializedView = new(view);
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
            SetObject(serializedView, "viewDataButton", viewDataButton);
            SetObject(serializedView, "saveEvidenceButton", saveEvidenceButton);
            SetObject(serializedView, "markResolvedButton", markResolvedButton);
            SetObject(serializedView, "resultPanel", resultPanel);
            SetObject(serializedView, "resultTitleText", resultTitleText);
            SetObject(serializedView, "resultDescriptionText", resultDescriptionText);
            SetObject(serializedView, "resultMetricsText", resultMetricsText);
            SetObject(serializedView, "returnToTitleButton", returnToTitleButton);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindFlowManager(
            GameFlowManager flowManager,
            DemoDatabase database,
            EvidenceManager evidenceManager,
            MainGameView view)
        {
            SerializedObject serializedFlow = new(flowManager);
            SetObject(serializedFlow, "database", database);
            SetObject(serializedFlow, "evidenceManager", evidenceManager);
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
            text.overflowMode = TextOverflowModes.Ellipsis;
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
