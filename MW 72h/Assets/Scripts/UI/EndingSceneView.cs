using MingBay.Core;
using MingBay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MingBay.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MingBay/UI/Ending Scene View")]
    public sealed class EndingSceneView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField]
        private EndingDatabase endingDatabase;

        [SerializeField]
        [Tooltip("结局场景运行时生成文本使用的中文字体。应绑定 Assets/UI/Fonts 中的 NotoSansSC-Regular SDF。")]
        private TMP_FontAsset fontAsset;

        [Header("Scene")]
        [SerializeField]
        private string titleSceneName = "TitleScene";

        [Header("Runtime UI")]
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text subtitleText;

        [SerializeField]
        private TMP_Text bodyText;

        [SerializeField]
        private TMP_Text metricsText;

        [SerializeField]
        private Button returnTitleButton;

        private bool isLoadingScene;

        private void Awake()
        {
            EnsureEventSystem();
            EnsureRuntimeUi();
        }

        private void OnEnable()
        {
            if (returnTitleButton != null)
            {
                returnTitleButton.onClick.AddListener(ReturnToTitle);
            }
        }

        private void OnDisable()
        {
            if (returnTitleButton != null)
            {
                returnTitleButton.onClick.RemoveListener(ReturnToTitle);
            }
        }

        private void Start()
        {
            RefreshEnding();
        }

        private void RefreshEnding()
        {
            EndingMetrics metrics = GameRunState.GetEndingMetrics();
            EndingDefinition ending = endingDatabase != null
                ? endingDatabase.Resolve(metrics)
                : null;

            if (ending != null)
            {
                titleText.text = ending.TitleCn;
                subtitleText.text = ending.SubtitleCn;
                bodyText.text = ending.BodyCn;
            }
            else
            {
                titleText.text = "结局：值班结束";
                subtitleText.text = "明湾通记录了这一夜的全部处理结果";
                bodyText.text = "当前项目缺少 EndingDatabase 配置，因此暂时显示默认结局。请在 EndingDatabase.asset 中配置结局文本和判定阈值。";
            }

            metricsText.text =
                $"A07风险值：{metrics.A07Risk}\n" +
                $"已处理工单：{metrics.ResolvedCount}\n" +
                $"转人工次数：{metrics.TransferCount}\n" +
                $"保留证据：{metrics.EvidenceCount}\n" +
                $"自动关闭：{metrics.AutoClearCount}\n" +
                $"最终对峙正确：{metrics.FinalCorrectCount}";
        }

        private void ReturnToTitle()
        {
            if (isLoadingScene)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
            {
                Debug.LogError($"Unable to load scene '{titleSceneName}'. Check Build Settings.", this);
                return;
            }

            isLoadingScene = true;
            SceneManager.LoadSceneAsync(titleSceneName);
        }

        private void EnsureRuntimeUi()
        {
            if (titleText != null &&
                subtitleText != null &&
                bodyText != null &&
                metricsText != null &&
                returnTitleButton != null)
            {
                return;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            TMP_FontAsset font = fontAsset != null
                ? fontAsset
                : FindSceneFont(canvas);
            RectTransform root = canvas.GetComponent<RectTransform>();

            RectTransform background = CreateImage(
                "EndingBackground",
                root,
                new Color(0.06f, 0.06f, 0.06f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
            background.SetAsFirstSibling();

            CreateImage(
                "EndingTopEdge",
                root,
                new Color(0.11f, 0.11f, 0.11f, 1f),
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, -4f),
                new Vector2(0f, 8f));

            RectTransform content = CreateRect(
                "EndingContent",
                root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(960f, 650f));

            titleText = CreateText(
                "Txt_EndingTitle",
                content,
                font,
                string.Empty,
                54f,
                FontStyles.Bold,
                Color.white,
                TextAlignmentOptions.Center,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -42f),
                new Vector2(0f, 80f));

            subtitleText = CreateText(
                "Txt_EndingSubtitle",
                content,
                font,
                string.Empty,
                26f,
                FontStyles.Bold,
                new Color(0.82f, 0.82f, 0.82f, 1f),
                TextAlignmentOptions.Center,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -122f),
                new Vector2(0f, 54f));

            bodyText = CreateText(
                "Txt_EndingBody",
                content,
                font,
                string.Empty,
                28f,
                FontStyles.Normal,
                new Color(0.88f, 0.88f, 0.88f, 1f),
                TextAlignmentOptions.Top,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 1f),
                new Vector2(90f, -410f),
                new Vector2(-90f, -190f));
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.lineSpacing = 12f;

            metricsText = CreateText(
                "Txt_EndingMetrics",
                content,
                font,
                string.Empty,
                24f,
                FontStyles.Bold,
                new Color(0.70f, 0.70f, 0.70f, 1f),
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 116f),
                new Vector2(520f, 150f));
            metricsText.textWrappingMode = TextWrappingModes.Normal;

            returnTitleButton = CreateButton(
                "Btn_ReturnTitle",
                content,
                font,
                "返回主菜单",
                30f,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 26f),
                new Vector2(320f, 76f));
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            _ = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        private static TMP_FontAsset FindSceneFont(Canvas canvas)
        {
            TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text != null && text.font != null)
                {
                    return text.font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            float fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.78f, 0.78f, 0.78f, 1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            CreateText(
                $"Txt_{name}",
                rect,
                font,
                label,
                fontSize,
                FontStyles.Bold,
                new Color(0.12f, 0.12f, 0.12f, 1f),
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        private static TMP_Text CreateText(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string text,
            float fontSize,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            return label;
        }

        private static RectTransform CreateImage(
            string name,
            RectTransform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject obj = new(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }
    }
}
