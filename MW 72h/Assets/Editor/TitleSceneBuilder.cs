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
    public static class TitleSceneBuilder
    {
        // 场景和字体资源路径。修改目录结构时需要同步更新这里。
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
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
        [MenuItem("明湾/场景工具/重新生成主菜单")]
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
            serializedController.FindProperty("gameSceneName").stringValue = "GameScene";
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
                new EditorBuildSettingsScene(GameScenePath, true)
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
}
