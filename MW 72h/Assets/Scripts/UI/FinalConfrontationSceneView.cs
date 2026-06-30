using System;
using System.Collections.Generic;
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
    [AddComponentMenu("MingBay/UI/Final Confrontation Scene View")]
    public sealed class FinalConfrontationSceneView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField]
        private FinalConfrontationDatabase database;

        [SerializeField]
        [Tooltip("最终对峙运行时生成文本使用的中文字体。")]
        private TMP_FontAsset fontAsset;

        [Header("Scene")]
        [SerializeField]
        private string endingSceneName = "EndingScene";

        [Header("Runtime UI")]
        [SerializeField]
        private TMP_Text stepText;

        [SerializeField]
        private TMP_Text statementText;

        [SerializeField]
        private TMP_Text goalText;

        [SerializeField]
        private TMP_Text replyText;

        [SerializeField]
        private Transform optionContentRoot;

        [SerializeField]
        private Button submitButton;

        [SerializeField]
        private Button nextButton;

        private readonly List<Button> optionButtons = new();
        private int questionIndex;
        private string selectedChainId;
        private bool hasSubmitted;
        private bool isLoadingScene;

        private void Awake()
        {
            EnsureEventSystem();
            EnsureRuntimeUi();
        }

        private void OnEnable()
        {
            if (submitButton != null)
            {
                submitButton.onClick.AddListener(SubmitAnswer);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(AdvanceQuestion);
            }
        }

        private void OnDisable()
        {
            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(SubmitAnswer);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(AdvanceQuestion);
            }
        }

        private void Start()
        {
            GameRunState.ResetFinalConfrontation();
            ShowQuestion();
        }

        private void ShowQuestion()
        {
            selectedChainId = string.Empty;
            hasSubmitted = false;

            if (database == null || database.Questions.Count == 0)
            {
                stepText.text = "最终对峙";
                statementText.text = "缺少最终对峙配置。";
                goalText.text = "请检查 FinalConfrontationDatabase.asset。";
                replyText.text = string.Empty;
                SetButtonText(nextButton, "查看结局");
                nextButton.gameObject.SetActive(true);
                submitButton.gameObject.SetActive(false);
                ClearOptions();
                return;
            }

            if (questionIndex >= database.Questions.Count)
            {
                LoadEndingScene();
                return;
            }

            FinalConfrontationQuestion question = database.Questions[questionIndex];
            stepText.text =
                $"最终对峙 {question.StepIndex}/{database.Questions.Count}";
            statementText.text = question.SystemStatementCn;
            goalText.text = question.PlayerGoalCn;
            replyText.text = string.Empty;
            SetButtonText(submitButton, "提交证据");
            SetButtonText(
                nextButton,
                questionIndex >= database.Questions.Count - 1
                    ? "查看结局"
                    : "下一题");
            submitButton.gameObject.SetActive(true);
            submitButton.interactable = false;
            nextButton.gameObject.SetActive(false);
            BuildEvidenceOptions(question);
        }

        private void BuildEvidenceOptions(FinalConfrontationQuestion question)
        {
            ClearOptions();
            IReadOnlyCollection<string> unlockedIds = GameRunState.GetUnlockedEvidenceIds();
            bool hasOption = false;

            foreach (FinalEvidenceOption option in database.EvidenceOptions)
            {
                if (option == null ||
                    !Contains(unlockedIds, option.ChainId) ||
                    !IsSelectable(question, option.ChainId))
                {
                    continue;
                }

                hasOption = true;
                Button button = CreateOptionButton(option);
                optionButtons.Add(button);
            }

            if (!hasOption)
            {
                TMP_Text emptyText = CreateText(
                    "Txt_EmptyEvidence",
                    optionContentRoot,
                    fontAsset,
                    "没有可出示的已解锁证据链。",
                    28,
                    FontStyles.Normal,
                    new Color(0.92f, 0.92f, 0.92f, 1f));
                emptyText.alignment = TextAlignmentOptions.Center;
            }
        }

        private Button CreateOptionButton(FinalEvidenceOption option)
        {
            RectTransform buttonRect = CreateRect(
                $"Btn_{option.ChainId}",
                optionContentRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 130f));
            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            SetButtonColors(button, image.color, new Color(0.25f, 0.25f, 0.25f, 1f));

            TMP_Text label = CreateText(
                "Label",
                buttonRect,
                fontAsset,
                $"{option.ChainId}\n{option.DisplayTextCn}",
                22,
                FontStyles.Normal,
                Color.white);
            label.alignment = TextAlignmentOptions.Left;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(18f, 10f);
            label.rectTransform.offsetMax = new Vector2(-18f, -10f);

            string chainId = option.ChainId;
            button.onClick.AddListener(() => SelectEvidence(chainId));
            return button;
        }

        private void SelectEvidence(string chainId)
        {
            if (hasSubmitted)
            {
                return;
            }

            selectedChainId = chainId;
            submitButton.interactable = !string.IsNullOrWhiteSpace(selectedChainId);
            foreach (Button button in optionButtons)
            {
                Image image = button.targetGraphic as Image;
                TMP_Text text = button.GetComponentInChildren<TMP_Text>();
                bool selected = text != null &&
                                text.text.StartsWith(
                                    selectedChainId,
                                    StringComparison.Ordinal);
                if (image != null)
                {
                    image.color = selected
                        ? new Color(0.05f, 0.45f, 0.18f, 1f)
                        : new Color(0.18f, 0.18f, 0.18f, 1f);
                }
            }
        }

        private void SubmitAnswer()
        {
            if (hasSubmitted ||
                database == null ||
                questionIndex >= database.Questions.Count ||
                string.IsNullOrWhiteSpace(selectedChainId))
            {
                return;
            }

            FinalConfrontationQuestion question = database.Questions[questionIndex];
            bool isCorrect = question.IsCorrect(selectedChainId);
            GameRunState.RecordFinalAnswer(isCorrect);
            hasSubmitted = true;
            replyText.text = isCorrect ? question.SuccessReplyCn : question.FailReplyCn;
            submitButton.interactable = false;
            nextButton.gameObject.SetActive(true);

            foreach (Button optionButton in optionButtons)
            {
                optionButton.interactable = false;
            }
        }

        private void AdvanceQuestion()
        {
            if (database == null || database.Questions.Count == 0)
            {
                LoadEndingScene();
                return;
            }

            questionIndex++;
            if (questionIndex >= database.Questions.Count)
            {
                LoadEndingScene();
                return;
            }

            ShowQuestion();
        }

        private void LoadEndingScene()
        {
            if (isLoadingScene)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(endingSceneName))
            {
                Debug.LogError($"Unable to load scene '{endingSceneName}'. Check Build Settings.", this);
                return;
            }

            isLoadingScene = true;
            SceneManager.LoadSceneAsync(endingSceneName);
        }

        private static bool IsSelectable(
            FinalConfrontationQuestion question,
            string chainId)
        {
            if (question == null || string.IsNullOrWhiteSpace(chainId))
            {
                return false;
            }

            if (string.Equals(
                    question.SelectableEvidenceChainIds,
                    "ALL_UNLOCKED",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] ids = question.SelectableEvidenceChainIds.Split(
                new[] { ',', ';', '|', '，', '；' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string id in ids)
            {
                if (string.Equals(id.Trim(), chainId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyCollection<string> values, string target)
        {
            if (values == null || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            foreach (string value in values)
            {
                if (string.Equals(value, target, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearOptions()
        {
            optionButtons.Clear();
            if (optionContentRoot == null)
            {
                return;
            }

            for (int index = optionContentRoot.childCount - 1; index >= 0; index--)
            {
                Destroy(optionContentRoot.GetChild(index).gameObject);
            }
        }

        private void EnsureRuntimeUi()
        {
            if (stepText != null &&
                statementText != null &&
                goalText != null &&
                replyText != null &&
                optionContentRoot != null &&
                submitButton != null &&
                nextButton != null)
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

            if (fontAsset == null)
            {
                fontAsset = FindSceneFont(canvas);
            }

            RectTransform root = canvas.GetComponent<RectTransform>();
            RectTransform background = CreateImage(
                "FinalBackground",
                root,
                new Color(0.06f, 0.06f, 0.06f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
            background.SetAsFirstSibling();

            RectTransform content = CreateRect(
                "FinalContent",
                root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1180f, 780f));

            stepText = CreateText(
                "Txt_FinalStep",
                content,
                fontAsset,
                string.Empty,
                42,
                FontStyles.Bold,
                Color.white);
            stepText.alignment = TextAlignmentOptions.Left;
            stepText.rectTransform.anchoredPosition = new Vector2(0f, 335f);
            stepText.rectTransform.sizeDelta = new Vector2(1120f, 70f);

            statementText = CreateText(
                "Txt_SystemStatement",
                content,
                fontAsset,
                string.Empty,
                30,
                FontStyles.Bold,
                new Color(0.92f, 0.92f, 0.92f, 1f));
            statementText.alignment = TextAlignmentOptions.Left;
            statementText.rectTransform.anchoredPosition = new Vector2(0f, 235f);
            statementText.rectTransform.sizeDelta = new Vector2(1120f, 120f);

            goalText = CreateText(
                "Txt_PlayerGoal",
                content,
                fontAsset,
                string.Empty,
                27,
                FontStyles.Normal,
                new Color(0.78f, 0.78f, 0.78f, 1f));
            goalText.alignment = TextAlignmentOptions.Left;
            goalText.rectTransform.anchoredPosition = new Vector2(0f, 125f);
            goalText.rectTransform.sizeDelta = new Vector2(1120f, 95f);

            RectTransform optionPanel = CreateImage(
                "EvidenceScroll",
                content,
                new Color(0.11f, 0.11f, 0.11f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -80f),
                new Vector2(1120f, 330f));
            ScrollRect scrollRect = optionPanel.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            RectTransform viewport = CreateImage(
                "Viewport",
                optionPanel,
                new Color(0f, 0f, 0f, 0f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            viewport.offsetMin = new Vector2(16f, 16f);
            viewport.offsetMax = new Vector2(-16f, -16f);
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform contentRoot = CreateRect(
                "OptionContent",
                viewport,
                new Vector2(0f, 1f),
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 0f));
            contentRoot.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = contentRoot;
            optionContentRoot = contentRoot;

            replyText = CreateText(
                "Txt_FinalReply",
                content,
                fontAsset,
                string.Empty,
                26,
                FontStyles.Normal,
                new Color(0.88f, 0.88f, 0.88f, 1f));
            replyText.alignment = TextAlignmentOptions.Center;
            replyText.rectTransform.anchoredPosition = new Vector2(0f, -285f);
            replyText.rectTransform.sizeDelta = new Vector2(1120f, 90f);

            submitButton = CreateButton(
                "Btn_SubmitEvidence",
                content,
                fontAsset,
                "提交证据",
                new Vector2(-155f, -370f),
                new Vector2(260f, 72f),
                new Color(0.88f, 0.1f, 0.08f, 1f));
            nextButton = CreateButton(
                "Btn_NextFinalQuestion",
                content,
                fontAsset,
                "下一题",
                new Vector2(155f, -370f),
                new Vector2(260f, 72f),
                new Color(0.08f, 0.18f, 0.72f, 1f));
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
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static RectTransform CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition,
                sizeDelta);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string text,
            float fontSize,
            FontStyles fontStyle,
            Color color)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(600f, 80f));
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string label,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            RectTransform rect = CreateImage(
                name,
                parent,
                color,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                sizeDelta);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            SetButtonColors(button, color, color * 1.1f);

            TMP_Text text = CreateText(
                "Label",
                rect,
                font,
                label,
                28,
                FontStyles.Bold,
                Color.white);
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetButtonText(Button button, string text)
        {
            TMP_Text label = button != null
                ? button.GetComponentInChildren<TMP_Text>(true)
                : null;
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void SetButtonColors(
            Button button,
            Color normalColor,
            Color highlightedColor)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = normalColor * 0.85f;
            colors.selectedColor = highlightedColor;
            colors.disabledColor = new Color(0.28f, 0.28f, 0.28f, 0.9f);
            button.colors = colors;
        }

        private static TMP_FontAsset FindSceneFont(Canvas canvas)
        {
            TMP_Text[] labels = canvas.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text label in labels)
            {
                if (label != null && label.font != null)
                {
                    return label.font;
                }
            }

            return TMP_Settings.defaultFontAsset;
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
    }
}
