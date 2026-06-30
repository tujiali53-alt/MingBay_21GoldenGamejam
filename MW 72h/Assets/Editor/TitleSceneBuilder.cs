using System.IO;
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
    /// 主菜单场景自动生成工具。
    /// 用于统一主菜单的基础层级、布局、配色和按钮绑定，避免多人手动搭建时出现差异。
    /// </summary>
    public static class TitleSceneBuilderLegacy
    {
        // 场景和字体资源路径。修改目录结构时需要同步更新这里。
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string Level1ScenePath = "Assets/Scenes/Level1Scene.unity";
        private const string DefaultFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // 主菜单基础色板。策划或 UI 调整整体色调时，优先修改这一组颜色。
        private static readonly Color Background = Hex("07111F");
        private static readonly Color Panel = Hex("0E1D2E");
        private static readonly Color PanelLight = Hex("142A40");
        private static readonly Color Accent = Hex("47D7C8");
        private static readonly Color AccentHover = Hex("65F0DF");
        private static readonly Color PrimaryText = Hex("EDF7F5");
        private static readonly Color MutedText = Hex("8CA7B7");
        private static readonly Color Warning = Hex("F6B85A");

        /// <summary>
        /// 从空场景重新生成完整主菜单，并更新 Build Settings。
        /// 注意：执行后会覆盖当前的 TitleScene。
        /// </summary>
        [MenuItem("明湾/场景工具/重新生成主菜单（旧版）")]
        public static void Build()
        {
            TMP_FontAsset font = EnsureTmpResources();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 创建场景运行所需的基础对象。
            CreateCamera();
            CreateEventSystem();

            // 采用 1920×1080 作为设计基准，并允许界面随分辨率等比缩放。
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
            CreateImage(
                "TopGlow",
                root,
                new Color(0.08f, 0.32f, 0.4f, 0.22f),
                new Vector2(0f, 0.58f),
                new Vector2(1f, 1f));
            CreateImage(
                "BottomShade",
                root,
                new Color(0f, 0f, 0f, 0.28f),
                Vector2.zero,
                new Vector2(1f, 0.22f));

            CreateTopBar(root, font);
            CreateMainContent(root, font, out Button startButton, out Button exitButton);
            CreateFooter(root, font);

            // 自动绑定运行时控制器，避免手动拖拽按钮引用。
            GameObject controllerObject = new("MainMenuController");
            MainMenuView controller = controllerObject.AddComponent<MainMenuView>();
            SerializedObject serializedController = new(controller);
            serializedController.FindProperty("startButton").objectReferenceValue = startButton;
            serializedController.FindProperty("exitButton").objectReferenceValue = exitButton;
            serializedController.FindProperty("gameSceneName").stringValue = "Level1Scene";
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, TitleScenePath);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CapturePreview(canvas, camera: Object.FindFirstObjectByType<Camera>());

            Debug.Log("TitleScene UI generated successfully.");
        }

        /// <summary>
        /// 输出一张主菜单预览图，方便不启动游戏也能快速检查布局。
        /// 预览图仅用于开发检查，Logs 目录不会提交到 Git。
        /// </summary>
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

            string previewDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(previewDirectory);
            string previewPath = Path.Combine(previewDirectory, "TitleScenePreview.png");
            File.WriteAllBytes(previewPath, preview.EncodeToPNG());
            Debug.Log($"TitleScene preview saved to: {previewPath}");

            canvas.renderMode = originalRenderMode;
            canvas.worldCamera = originalWorldCamera;
            camera.targetTexture = originalTarget;
            RenderTexture.active = originalActive;

            Object.DestroyImmediate(preview);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
        }

        /// <summary>
        /// 获取主菜单使用的 TextMeshPro 字体，并在缺失时给出明确错误。
        /// </summary>
        private static TMP_FontAsset EnsureTmpResources()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
            if (font != null)
            {
                return font;
            }

            throw new System.IO.FileNotFoundException(
                "TMP Essential Resources are missing from the project.",
                DefaultFontPath);
        }

        /// <summary>
        /// 创建只负责清屏的正交摄像机。
        /// </summary>
        private static void CreateCamera()
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;
        }

        /// <summary>
        /// 创建 UGUI 按钮交互所需的事件系统。
        /// </summary>
        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        /// <summary>
        /// 创建顶部品牌栏和 A-07 在线状态。
        /// </summary>
        private static void CreateTopBar(RectTransform root, TMP_FontAsset font)
        {
            RectTransform topBar = CreateImage(
                "TopBar",
                root,
                new Color(0.025f, 0.07f, 0.11f, 0.94f),
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, 76f),
                new Vector2(0f, -38f));

            CreateImage(
                "AccentLine",
                topBar,
                Accent,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 2f),
                Vector2.zero);

            CreateText(
                "Txt_Brand",
                topBar,
                font,
                "MING BAY CITY SERVICE // QUALITY CONTROL",
                24f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f),
                new Vector2(0.7f, 1f),
                new Vector2(56f, 0f),
                new Vector2(-16f, 0f));

            RectTransform status = CreateImage(
                "StatusPanel",
                topBar,
                new Color(0.12f, 0.35f, 0.34f, 0.62f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(248f, 38f),
                new Vector2(-152f, 0f));

            CreateText(
                "Txt_Status",
                status,
                font,
                "●  A-07  ONLINE",
                18f,
                FontStyles.Bold,
                Accent,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
        }

        /// <summary>
        /// 创建主标题、值班信息卡和两个主要操作按钮。
        /// </summary>
        private static void CreateMainContent(
            RectTransform root,
            TMP_FontAsset font,
            out Button startButton,
            out Button exitButton)
        {
            RectTransform left = CreateRect(
                "TitleBlock",
                root,
                new Vector2(0f, 0f),
                new Vector2(0.62f, 1f),
                new Vector2(64f, -20f),
                new Vector2(-180f, -180f));

            RectTransform titleAccent = CreateImage(
                "TitleAccent",
                left,
                Accent,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(6f, 354f),
                new Vector2(0f, 12f));

            CreateText(
                "Txt_Eyebrow",
                left,
                font,
                "FINAL 72 HOURS // NIGHT SHIFT",
                22f,
                FontStyles.Bold,
                Accent,
                TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0.68f),
                new Vector2(1f, 0.8f),
                new Vector2(42f, 0f),
                new Vector2(-20f, 0f));

            CreateText(
                "Txt_Title",
                left,
                font,
                "MING BAY",
                94f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.38f),
                new Vector2(1f, 0.7f),
                new Vector2(38f, 0f),
                new Vector2(-8f, 0f));

            CreateText(
                "Txt_72H",
                left,
                font,
                "72H",
                76f,
                FontStyles.Bold,
                Warning,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.2f),
                new Vector2(1f, 0.43f),
                new Vector2(42f, 0f),
                new Vector2(-12f, 0f));

            CreateText(
                "Txt_Description",
                left,
                font,
                "Review unresolved citizen requests.\nVerify the records. Decide what remains visible.",
                26f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f),
                new Vector2(0.88f, 0.22f),
                new Vector2(42f, 0f),
                new Vector2(-12f, 0f));

            RectTransform card = CreateImage(
                "ShiftAccessPanel",
                root,
                new Color(Panel.r, Panel.g, Panel.b, 0.96f),
                new Vector2(0.67f, 0.5f),
                new Vector2(0.67f, 0.5f),
                new Vector2(500f, 590f),
                new Vector2(110f, -20f));
            card.gameObject.AddComponent<Outline>().effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.3f);

            CreateText(
                "Txt_AccessTitle",
                card,
                font,
                "SHIFT ACCESS",
                30f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.81f),
                new Vector2(1f, 1f),
                new Vector2(42f, 0f),
                new Vector2(-42f, -4f));

            CreateText(
                "Txt_ShiftInfo",
                card,
                font,
                "OPERATOR       A-07\nCLEARANCE      TEMPORARY\nQUEUE STATUS   WAITING\nSYSTEM MODE    QUALITY REVIEW",
                20f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.48f),
                new Vector2(1f, 0.82f),
                new Vector2(42f, 0f),
                new Vector2(-42f, 0f));

            CreateImage(
                "Divider",
                card,
                new Color(0.4f, 0.65f, 0.7f, 0.25f),
                new Vector2(0f, 0.47f),
                new Vector2(1f, 0.47f),
                new Vector2(-84f, 1f),
                Vector2.zero);

            startButton = CreateButton(
                "Btn_Start",
                card,
                font,
                "START SHIFT",
                Accent,
                AccentHover,
                Background,
                new Vector2(0.5f, 0.31f),
                new Vector2(416f, 82f));

            exitButton = CreateButton(
                "Btn_Exit",
                card,
                font,
                "EXIT SYSTEM",
                PanelLight,
                Hex("1D3A53"),
                PrimaryText,
                new Vector2(0.5f, 0.13f),
                new Vector2(416f, 70f));

            CreateText(
                "Txt_Hint",
                card,
                font,
                "All operator actions are recorded.",
                16f,
                FontStyles.Italic,
                new Color(MutedText.r, MutedText.g, MutedText.b, 0.74f),
                TextAlignmentOptions.Center,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.065f),
                new Vector2(24f, 0f),
                new Vector2(-24f, 0f));
        }

        /// <summary>
        /// 创建底部平台名称和版本信息。
        /// </summary>
        private static void CreateFooter(RectTransform root, TMP_FontAsset font)
        {
            CreateText(
                "Txt_FooterLeft",
                root,
                font,
                "MING BAY UNIFIED LIFE SERVICE PLATFORM",
                15f,
                FontStyles.Normal,
                new Color(MutedText.r, MutedText.g, MutedText.b, 0.65f),
                TextAlignmentOptions.BottomLeft,
                Vector2.zero,
                new Vector2(0.7f, 0.08f),
                new Vector2(56f, 22f),
                new Vector2(-20f, 0f));

            CreateText(
                "Txt_Version",
                root,
                font,
                "DEMO BUILD 0.1",
                15f,
                FontStyles.Normal,
                new Color(MutedText.r, MutedText.g, MutedText.b, 0.65f),
                TextAlignmentOptions.BottomRight,
                new Vector2(0.7f, 0f),
                new Vector2(1f, 0.08f),
                new Vector2(20f, 22f),
                new Vector2(-56f, 0f));
        }

        /// <summary>
        /// 创建带统一交互颜色的标准按钮。
        /// </summary>
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
            colors.pressedColor = Color.Lerp(highlightedColor, Background, 0.2f);
            colors.selectedColor = highlightedColor;
            colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            CreateText(
                "Label",
                rect,
                font,
                label,
                24f,
                FontStyles.Bold,
                textColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return button;
        }

        /// <summary>
        /// 创建纯色 Image，供背景、面板和分隔线复用。
        /// </summary>
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

        /// <summary>
        /// 创建 TextMeshPro 文本并应用统一的基础设置。
        /// </summary>
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
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        /// <summary>
        /// 创建并初始化 RectTransform。
        /// 参数顺序统一为锚点、位置、尺寸，方便批量调整布局。
        /// </summary>
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

        /// <summary>
        /// 保证启动顺序为主菜单在前、游戏场景在后。
        /// </summary>
        private static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(Level1ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/Level2Scene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Level3Scene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/FinalConfrontationScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/EndingScene.unity", true)
            };
        }

        /// <summary>
        /// 将十六进制颜色字符串转换为 Unity Color。
        /// </summary>
        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString($"#{value}", out Color color))
            {
                throw new System.ArgumentException($"Invalid color value: {value}");
            }

            return color;
        }
    }

    /// <summary>
    /// 新版标题界面生成器。
    /// 生成黑底左侧文字主菜单：标题、开始、继续、设置、开发人员和退出按钮。
    /// </summary>
    public static class TitleSceneBuilder
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string Level1ScenePath = "Assets/Scenes/Level1Scene.unity";
        private static readonly string[] FontAssetPaths =
        {
            "Assets/UI/Fonts/NotoSansSC-Regular SDF.asset",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"
        };

        private static readonly Color Background = Hex("111111");
        private static readonly Color OuterVoid = Hex("0C0C0C");
        private static readonly Color TerminalBorder = Hex("3C3C3C");
        private static readonly Color DesktopSurface = Hex("222222");
        private static readonly Color TaskbarColor = Hex("575757");
        private static readonly Color TaskbarDark = Hex("3B3B3B");
        private static readonly Color IconPlate = Hex("8A8A8A");
        private static readonly Color ActiveButton = Hex("C7E6D8");
        private static readonly Color PanelGrey = Hex("D0D0D0");
        private static readonly Color PrimaryText = Hex("F1F1F1");
        private static readonly Color MutedText = Hex("B8B8B8");
        private static readonly Color DimText = Hex("747474");
        private static readonly Color AlertRed = Hex("E04D73");
        private static readonly Color WarningRed = Hex("F08B91");

        [MenuItem("明湾/场景工具/重新生成主菜单")]
        public static void Build()
        {
            TMP_FontAsset font = EnsureTmpResources();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
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
            CreateImage(
                "TopEdge",
                root,
                Hex("1B1B1B"),
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, 8f),
                new Vector2(0f, -4f));

            CreateMinimalTitleMenu(
                root,
                font,
                out Button startButton,
                out Button continueButton,
                out Button settingsButton,
                out Button developersButton,
                out Button exitButton);
            Button languageButton = CreateLanguagePlaceholderButton(root, font);
            BindMenuController(startButton, continueButton, settingsButton, languageButton, developersButton, exitButton);

            EditorSceneManager.SaveScene(scene, TitleScenePath);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("TitleScene main menu UI generated successfully.");
        }

        private static void BindMenuController(
            Button startButton,
            Button continueButton,
            Button settingsButton,
            Button languageButton,
            Button developersButton,
            Button exitButton)
        {
            GameObject controllerObject = new("MainMenuController");
            MainMenuView controller = controllerObject.AddComponent<MainMenuView>();
            SerializedObject serializedController = new(controller);
            serializedController.FindProperty("startButton").objectReferenceValue = startButton;

            SerializedProperty additionalStartButtons =
                serializedController.FindProperty("additionalStartButtons");
            additionalStartButtons.arraySize = 1;
            additionalStartButtons.GetArrayElementAtIndex(0).objectReferenceValue = continueButton;

            serializedController.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            serializedController.FindProperty("languageButton").objectReferenceValue = languageButton;
            serializedController.FindProperty("developersButton").objectReferenceValue = developersButton;
            serializedController.FindProperty("exitButton").objectReferenceValue = exitButton;
            serializedController.FindProperty("gameSceneName").stringValue = "Level1Scene";
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateMinimalTitleMenu(
            RectTransform root,
            TMP_FontAsset font,
            out Button startButton,
            out Button continueButton,
            out Button settingsButton,
            out Button developersButton,
            out Button exitButton)
        {
            RectTransform menu = CreateRect(
                "TitleMenu",
                root,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(136f, -120f),
                new Vector2(620f, 430f));
            menu.pivot = new Vector2(0f, 0.5f);
            menu.anchoredPosition = new Vector2(136f, -120f);

            CreateText(
                "Txt_Title",
                menu,
                font,
                "明湾72h",
                72f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -92f),
                Vector2.zero);

            startButton = CreateTextMenuButton(
                "Btn_StartNewGame",
                menu,
                font,
                "开始新游戏",
                54f,
                new Vector2(0f, -148f),
                new Vector2(430f, 68f));
            continueButton = CreateTextMenuButton(
                "Btn_ContinueGame",
                menu,
                font,
                "继续游戏",
                38f,
                new Vector2(0f, -228f),
                new Vector2(320f, 48f));
            settingsButton = CreateTextMenuButton(
                "Btn_Settings",
                menu,
                font,
                "设置",
                38f,
                new Vector2(0f, -282f),
                new Vector2(240f, 48f));
            developersButton = CreateTextMenuButton(
                "Btn_Developers",
                menu,
                font,
                "开发人员名单",
                38f,
                new Vector2(0f, -336f),
                new Vector2(360f, 48f));
            exitButton = CreateTextMenuButton(
                "Btn_ExitGame",
                menu,
                font,
                "退出游戏",
                38f,
                new Vector2(0f, -390f),
                new Vector2(300f, 48f));
        }

        private static Button CreateTextMenuButton(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            float fontSize,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                anchoredPosition,
                sizeDelta);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;

            Image target = rect.gameObject.AddComponent<Image>();
            target.color = new Color(1f, 1f, 1f, 0f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            CreateText(
                $"Txt_{name}",
                rect,
                font,
                label,
                fontSize,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return button;
        }

        private static Button CreateLanguagePlaceholderButton(RectTransform root, TMP_FontAsset font)
        {
            RectTransform rect = CreateRect(
                "Btn_LanguageToggle",
                root,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-72f, -72f),
                new Vector2(56f, 56f));
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image target = rect.gameObject.AddComponent<Image>();
            target.color = new Color(0.78f, 0.78f, 0.78f, 0.18f);

            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.92f, 0.92f, 0.92f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.10f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.22f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.32f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            CreateText(
                "Txt_LanguageToggle",
                rect,
                font,
                "中",
                22f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return button;
        }

        private static void CreateDesktopTerminal(
            RectTransform root,
            TMP_FontAsset font,
            out Button workAppButton,
            out Button taskQueueButton,
            out TMP_Text clockText)
        {
            RectTransform terminalFrame = CreateImage(
                "MWT_TerminalFrame",
                root,
                TerminalBorder,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1500f, 760f),
                new Vector2(0f, -8f));

            RectTransform desktop = CreateImage(
                "DesktopSurface",
                terminalFrame,
                DesktopSurface,
                Vector2.zero,
                Vector2.one,
                new Vector2(-26f, -26f),
                Vector2.zero);

            CreateCountdownBar(desktop, font);
            workAppButton = CreateDesktopIcons(desktop, font);
            CreateCenterLogo(desktop, font);
            CreateMentorBubble(desktop, font);
            CreateCursor(desktop, font);
            CreateInputMethodWarning(desktop, font);
            taskQueueButton = CreateBottomTaskbar(desktop, font, out clockText);
            CreateGlobalStatusLayer(root);
        }

        private static void CreateCountdownBar(RectTransform desktop, TMP_FontAsset font)
        {
            RectTransform track = CreateImage(
                "CountdownTrack",
                desktop,
                Hex("777777"),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(700f, 22f),
                new Vector2(0f, -18f));

            CreateImage(
                "CountdownElapsed",
                track,
                AlertRed,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(220f, 14f),
                new Vector2(111f, 0f));

            CreateImage(
                "CountdownTail",
                track,
                new Color(0.82f, 0.82f, 0.82f, 0.75f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(64f, 3f),
                new Vector2(-34f, 0f));

            CreateText(
                "Txt_Countdown",
                track,
                font,
                "T-72h",
                18f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
        }

        private static Button CreateDesktopIcons(RectTransform desktop, TMP_FontAsset font)
        {
            Button workAppButton = CreateDesktopIcon(
                "Btn_WorkApp",
                desktop,
                font,
                "icon",
                "工单APP",
                new Vector2(92f, -110f),
                true);

            CreateDesktopIcon(
                "Btn_ClueNotebook",
                desktop,
                font,
                "icon",
                "线索笔记",
                new Vector2(92f, -224f),
                false);

            return workAppButton;
        }

        private static Button CreateDesktopIcon(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string iconText,
            string label,
            Vector2 anchoredPosition,
            bool interactable)
        {
            RectTransform iconRoot = CreateRect(
                $"{name}_Group",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                anchoredPosition,
                new Vector2(96f, 108f));

            RectTransform buttonRect = CreateImage(
                name,
                iconRoot,
                interactable ? Hex("303030") : IconPlate,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(58f, 58f),
                new Vector2(0f, -32f));

            Outline outline = buttonRect.gameObject.AddComponent<Outline>();
            outline.effectColor = interactable
                ? new Color(0.86f, 0.86f, 0.86f, 0.5f)
                : new Color(0.86f, 0.86f, 0.86f, 0.18f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonRect.GetComponent<Image>();
            button.interactable = interactable;
            ApplyDesktopButtonColors(button, interactable);

            CreateText(
                "Txt_Icon",
                buttonRect,
                font,
                iconText,
                13f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            CreateText(
                "Txt_Label",
                iconRoot,
                font,
                label,
                16f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Top,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.34f),
                Vector2.zero,
                Vector2.zero);

            return button;
        }

        private static void CreateCenterLogo(RectTransform desktop, TMP_FontAsset font)
        {
            RectTransform logoRoot = CreateRect(
                "LogoGroup",
                desktop,
                new Vector2(0.5f, 0.58f),
                new Vector2(0.5f, 0.58f),
                Vector2.zero,
                new Vector2(620f, 170f));

            CreateLogoMark(logoRoot);

            CreateText(
                "Txt_MingBay",
                logoRoot,
                font,
                "明 湾 通",
                62f,
                FontStyles.Bold,
                new Color(0.72f, 0.72f, 0.72f, 0.34f),
                TextAlignmentOptions.Center,
                new Vector2(0f, 0.38f),
                Vector2.one,
                new Vector2(82f, 0f),
                Vector2.zero);

            CreateText(
                "Txt_TerminalVersion",
                logoRoot,
                font,
                "MWT-TERMINAL v2.7.1",
                26f,
                FontStyles.Bold,
                new Color(0.72f, 0.72f, 0.72f, 0.28f),
                TextAlignmentOptions.Center,
                Vector2.zero,
                new Vector2(1f, 0.42f),
                new Vector2(82f, 0f),
                Vector2.zero);
        }

        private static void CreateLogoMark(RectTransform logoRoot)
        {
            int[,] pattern =
            {
                {0, 1, 1, 1, 0},
                {1, 0, 0, 0, 1},
                {1, 0, 1, 0, 1},
                {1, 0, 0, 0, 1},
                {0, 1, 1, 1, 0}
            };

            RectTransform mark = CreateRect(
                "LogoPixelMark",
                logoRoot,
                new Vector2(0f, 0.44f),
                new Vector2(0f, 0.44f),
                new Vector2(72f, 24f),
                new Vector2(78f, 78f));

            const float cell = 11f;
            const float gap = 3f;
            for (int row = 0; row < 5; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    if (pattern[row, column] == 0)
                    {
                        continue;
                    }

                    CreateImage(
                        $"LogoCell_{column}_{row}",
                        mark,
                        new Color(0.72f, 0.72f, 0.72f, 0.28f),
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(cell, cell),
                        new Vector2(12f + column * (cell + gap), -12f - row * (cell + gap)));
                }
            }
        }
        private static void CreateMentorBubble(RectTransform desktop, TMP_FontAsset font)
        {
            RectTransform bubble = CreateImage(
                "MentorBubble",
                desktop,
                PanelGrey,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(520f, 96f),
                new Vector2(215f, 130f));

            RectTransform avatar = CreateImage(
                "MentorAvatar",
                bubble,
                Hex("242424"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(66f, 66f),
                new Vector2(48f, 0f));

            CreateText(
                "Txt_AvatarFace",
                avatar,
                font,
                "A07",
                16f,
                FontStyles.Bold,
                MutedText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 14f));

            CreateText(
                "Txt_AvatarName",
                avatar,
                font,
                "KKK",
                16f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Bottom,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, -4f));

            CreateText(
                "Txt_MentorHint",
                bubble,
                font,
                "先点击工单APP，查看今日任务",
                20f,
                FontStyles.Bold,
                Hex("181818"),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f),
                Vector2.one,
                new Vector2(110f, 0f),
                new Vector2(-26f, 0f));
        }

        private static void CreateCursor(RectTransform desktop, TMP_FontAsset font)
        {
            CreateText(
                "Txt_CustomCursor",
                desktop,
                font,
                "↖",
                34f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                new Vector2(0.72f, 0.36f),
                new Vector2(0.72f, 0.36f),
                new Vector2(-22f, -22f),
                new Vector2(22f, 22f));
        }

        private static void CreateInputMethodWarning(RectTransform desktop, TMP_FontAsset font)
        {
            RectTransform inputPanel = CreateImage(
                "InputMethod_A07Warning",
                desktop,
                Hex("8C8C8C"),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(180f, 38f),
                new Vector2(-114f, 112f));

            Outline outline = inputPanel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(AlertRed.r, AlertRed.g, AlertRed.b, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);

            CreateImage(
                "AbnormalCaret",
                inputPanel,
                AlertRed,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(14f, 3f),
                new Vector2(-10f, 0f));

            CreateText(
                "Txt_InputState",
                inputPanel,
                font,
                "中",
                16f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 0f),
                new Vector2(-12f, 0f));
        }

        private static Button CreateBottomTaskbar(
            RectTransform desktop,
            TMP_FontAsset font,
            out TMP_Text clockText)
        {
            RectTransform taskbar = CreateImage(
                "Taskbar",
                desktop,
                TaskbarColor,
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(0f, 70f),
                new Vector2(0f, 35f));

            CreateWindowsGlyph(taskbar);

            Button taskQueueButton = CreateTaskbarButton(
                "Btn_TaskQueue",
                taskbar,
                font,
                "工单队列",
                new Vector2(120f, 0f),
                ActiveButton,
                Hex("1B1B1B"),
                true);

            CreateTaskbarButton(
                "Btn_Database",
                taskbar,
                font,
                "资料库",
                new Vector2(365f, 0f),
                Hex("D5D5D5"),
                Hex("1B1B1B"),
                false);

            clockText = CreateRiskAndClock(taskbar, font);
            return taskQueueButton;
        }

        private static void CreateWindowsGlyph(RectTransform taskbar)
        {
            RectTransform glyph = CreateRect(
                "SystemGlyph",
                taskbar,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(38f, 0f),
                new Vector2(28f, 28f));

            Color glyphColor = new Color(0.82f, 0.91f, 0.94f, 0.95f);
            CreateImage("Pane01", glyph, glyphColor, new Vector2(0f, 0.52f), new Vector2(0.44f, 1f));
            CreateImage("Pane02", glyph, glyphColor, new Vector2(0.52f, 0.52f), Vector2.one);
            CreateImage("Pane03", glyph, glyphColor, Vector2.zero, new Vector2(0.44f, 0.44f));
            CreateImage("Pane04", glyph, glyphColor, new Vector2(0.52f, 0f), new Vector2(1f, 0.44f));
        }

        private static Button CreateTaskbarButton(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            Vector2 anchoredPosition,
            Color background,
            Color textColor,
            bool interactable)
        {
            RectTransform rect = CreateImage(
                name,
                parent,
                background,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(210f, 42f),
                anchoredPosition);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.interactable = interactable;

            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, PrimaryText, 0.12f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.14f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = background;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            RectTransform miniIcon = CreateImage(
                "MiniIcon",
                rect,
                Hex("EFEFEF"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, 28f),
                new Vector2(22f, 0f));

            CreateText(
                "Txt_MiniIcon",
                miniIcon,
                font,
                "icon",
                8f,
                FontStyles.Bold,
                Hex("202020"),
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            CreateText(
                "Txt_Label",
                rect,
                font,
                label,
                15f,
                FontStyles.Bold,
                textColor,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(58f, 0f),
                new Vector2(-8f, 0f));

            return button;
        }

        private static TMP_Text CreateRiskAndClock(RectTransform taskbar, TMP_FontAsset font)
        {
            RectTransform riskRoot = CreateRect(
                "A07RiskCluster",
                taskbar,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-330f, 0f),
                new Vector2(250f, 46f));

            RectTransform warning = CreateImage(
                "WarningTriangle",
                riskRoot,
                WarningRed,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(30f, 30f),
                new Vector2(15f, 0f));

            CreateText(
                "Txt_Warning",
                warning,
                font,
                "!",
                22f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            CreateText(
                "Txt_RiskLabel",
                riskRoot,
                font,
                "A07 异常指示",
                13f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(48f, 2f),
                Vector2.zero);

            RectTransform track = CreateImage(
                "RiskTrack",
                riskRoot,
                Hex("2F2F2F"),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(136f, 10f),
                new Vector2(118f, 11f));

            for (int index = 0; index < 11; index++)
            {
                CreateImage(
                    $"RiskSegment_{index:00}",
                    track,
                    index < 8 ? AlertRed : DimText,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(8f, 10f),
                    new Vector2(6f + index * 11f, 0f));
            }

            RectTransform clock = CreateImage(
                "ClockPanel",
                taskbar,
                TaskbarDark,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(144f, 42f),
                new Vector2(-92f, 0f));

            TMP_Text clockText = CreateText(
                "Txt_Clock",
                clock,
                font,
                "下午9:10",
                16f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 7f));

            CreateImage(
                "ClockUnderline",
                clock,
                PrimaryText,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(42f, 3f),
                new Vector2(0f, 10f));

            return clockText;
        }

        private static void CreateGlobalStatusLayer(RectTransform root)
        {
            CreateImage(
                "AlwaysOnTopStatusLayer",
                root,
                Hex("6A6A6A"),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(600f, 36f),
                new Vector2(0f, 112f));
        }

        private static void ApplyDesktopButtonColors(Button button, bool interactable)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = interactable
                ? Hex("303030")
                : IconPlate;
            colors.highlightedColor = interactable
                ? Hex("464646")
                : IconPlate;
            colors.pressedColor = interactable
                ? Hex("242424")
                : IconPlate;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = interactable ? colors.normalColor : IconPlate;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static TMP_FontAsset EnsureTmpResources()
        {
            foreach (string fontPath in FontAssetPaths)
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
                if (font != null)
                {
                    return font;
                }
            }

            throw new FileNotFoundException(
                "TMP font assets are missing from the project.",
                FontAssetPaths[0]);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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

            string previewDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(previewDirectory);
            string previewPath = Path.Combine(previewDirectory, "TitleScenePreview.png");
            File.WriteAllBytes(previewPath, preview.EncodeToPNG());
            Debug.Log($"TitleScene preview saved to: {previewPath}");

            canvas.renderMode = originalRenderMode;
            canvas.worldCamera = originalWorldCamera;
            camera.targetTexture = originalTarget;
            RenderTexture.active = originalActive;

            Object.DestroyImmediate(preview);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
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
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
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
                new EditorBuildSettingsScene(Level1ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/Level2Scene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Level3Scene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/FinalConfrontationScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/EndingScene.unity", true)
            };
        }

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString($"#{value}", out Color color))
            {
                throw new System.ArgumentException($"Invalid color value: {value}");
            }

            return color;
        }
    }
}
