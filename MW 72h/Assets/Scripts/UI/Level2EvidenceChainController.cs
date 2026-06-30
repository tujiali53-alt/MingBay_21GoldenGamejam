using System;
using System.Collections.Generic;
using System.Text;
using MingBay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MingBay.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MingBay/Level2/UI/Evidence Chain Controller")]
    public sealed class Level2EvidenceChainController : MonoBehaviour
    {
        private const string InsufficientAnalysisText =
            "证据不足，提交可能会引起用户不满";

        private static readonly Color OverlayColor = new(0f, 0f, 0f, 0.72f);
        private static readonly Color PanelColor = new(0.79f, 0.79f, 0.77f, 1f);
        private static readonly Color PanelDark = new(0.30f, 0.30f, 0.30f, 1f);
        private static readonly Color TextColor = new(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color MutedTextColor = new(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color AccentGreen = new(0.20f, 0.68f, 0.42f, 1f);
        private static readonly Color AccentYellow = new(0.95f, 0.64f, 0.15f, 1f);
        private static readonly Color ButtonBlue = new(0.30f, 0.40f, 0.78f, 1f);
        private static readonly Color ButtonGray = new(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color ListItemColor = new(0.86f, 0.86f, 0.82f, 1f);
        private static readonly Color ListItemBorderColor = new(0.18f, 0.18f, 0.18f, 0.35f);

        private readonly HashSet<string> collectedSlotIds = new();
        private readonly List<Level2KeywordSlot> collectedSlots = new();
        private readonly Dictionary<string, string> slotAssignments = new();
        private readonly List<Button> collectedKeywordButtons = new();
        private readonly List<Button> answerSlotButtons = new();

        private Canvas canvas;
        private TMP_FontAsset font;
        private GameObject statusPanel;
        private TMP_Text statusText;
        private GameObject analysisOverlay;
        private RectTransform slotListRoot;
        private RectTransform keywordListRoot;
        private ScrollRect keywordScrollRect;
        private TMP_Text analysisText;
        private Button analysisButton;
        private Button closeButton;
        private Action<bool> analysisClosed;
        private Level2EvidenceChain currentChain;
        private string selectedKeywordSlotId;
        private bool analysisReady;
        private bool analysisMatched;
        private string currentAiSuggestion;

        public bool HasCollectedKeywords => collectedSlotIds.Count > 0;
        public bool HasAnalyzed => analysisReady;
        public bool IsAnalysisMatched => analysisMatched;
        public string CurrentAiSuggestion => currentAiSuggestion;

        public void HideTransientPanels()
        {
            SetVisible(statusPanel, false);
            SetVisible(analysisOverlay, false);
            selectedKeywordSlotId = string.Empty;
            analysisClosed = null;
        }

        public void BeginTicket(TicketData ticket, Level2EvidenceChain chain)
        {
            currentChain = chain;
            collectedSlotIds.Clear();
            collectedSlots.Clear();
            slotAssignments.Clear();
            selectedKeywordSlotId = string.Empty;
            analysisReady = false;
            analysisMatched = false;
            currentAiSuggestion = string.Empty;
            analysisClosed = null;
            SetVisible(statusPanel, false);
            SetVisible(analysisOverlay, false);
        }

        public string FormatEvidenceDetailText(int panelIndex, string rawText)
        {
            if (currentChain == null || string.IsNullOrWhiteSpace(rawText))
            {
                return rawText;
            }

            string formatted = rawText;
            List<Level2KeywordSlot> slots = GetSlotsForPanel(panelIndex);
            slots.Sort((left, right) =>
                (right.Keyword?.Length ?? 0).CompareTo(left.Keyword?.Length ?? 0));

            foreach (Level2KeywordSlot slot in slots)
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.Keyword))
                {
                    continue;
                }

                string replacement = collectedSlotIds.Contains(slot.SlotId)
                    ? $"<link=\"{slot.SlotId}\"><color=#34AD6B>{slot.Keyword}</color></link>"
                    : $"<link=\"{slot.SlotId}\">{slot.Keyword}</link>";
                formatted = formatted.Replace(slot.Keyword, replacement);
            }

            return formatted;
        }

        public bool CollectKeywordBySlotId(string slotId)
        {
            Level2KeywordSlot slot = FindSlot(slotId);
            if (slot == null)
            {
                return false;
            }

            bool added = collectedSlotIds.Add(slot.SlotId);
            if (added)
            {
                collectedSlots.Add(slot);
            }

            return added;
        }

        public bool IsCurrentChainSatisfied()
        {
            return currentChain != null && AreAssignmentsPerfect();
        }

        public void ShowAnalysisPanel(Action<bool> onClosed)
        {
            EnsureRuntimeUi();
            analysisClosed = onClosed;
            selectedKeywordSlotId = string.Empty;
            analysisReady = false;
            analysisMatched = false;
            currentAiSuggestion = string.Empty;
            analysisText.text = "请选择右侧关键词，再点击左侧对应槽位的答案框。";
            analysisButton.interactable = true;
            closeButton.interactable = true;
            RebuildAnalysisPanel();
            if (keywordScrollRect != null)
            {
                keywordScrollRect.verticalNormalizedPosition = 1f;
            }

            SetVisible(analysisOverlay, true);
            analysisOverlay.transform.SetAsLastSibling();
        }

        private void RunAnalysis()
        {
            analysisMatched = AreAssignmentsPerfect();
            currentAiSuggestion = analysisMatched && currentChain != null
                ? currentChain.AiSuggestionText
                : InsufficientAnalysisText;
            analysisReady = true;
            analysisText.text = currentAiSuggestion;
        }

        private void CloseAnalysisPanel()
        {
            SetVisible(analysisOverlay, false);
            Action<bool> callback = analysisClosed;
            analysisClosed = null;
            callback?.Invoke(analysisReady);
        }

        private bool AreAssignmentsPerfect()
        {
            if (currentChain == null)
            {
                return false;
            }

            foreach (AnswerSlotInfo answerSlot in GetAnswerSlots())
            {
                if (string.IsNullOrWhiteSpace(answerSlot.SlotId) ||
                    !slotAssignments.TryGetValue(answerSlot.SlotId, out string assignedKeywordId))
                {
                    return false;
                }

                Level2KeywordSlot assignedSlot = FindSlot(assignedKeywordId);
                if (assignedSlot == null ||
                    !string.Equals(
                        assignedSlot.AnswerSlotId,
                        answerSlot.SlotId,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private void SelectCollectedKeyword(string slotId)
        {
            selectedKeywordSlotId = slotId;
            RebuildAnalysisPanel();
        }

        private void AssignSelectedKeywordToSlot(string answerSlotId)
        {
            if (string.IsNullOrWhiteSpace(selectedKeywordSlotId))
            {
                analysisText.text = "请先点击右侧已收集关键词。";
                return;
            }

            slotAssignments[answerSlotId] = selectedKeywordSlotId;
            selectedKeywordSlotId = string.Empty;
            analysisReady = false;
            analysisMatched = false;
            currentAiSuggestion = string.Empty;
            analysisText.text = "槽位已填入。继续补全证据链，或点击 AI分析。";
            RebuildAnalysisPanel();
        }

        private void ShowKeywordStatus(string message)
        {
            EnsureRuntimeUi();
            SetVisible(statusPanel, true);
            statusPanel.transform.SetAsLastSibling();
            statusText.text = $"{message}\n\n{BuildCollectedKeywordText()}";
        }

        private string BuildCollectedKeywordText()
        {
            if (collectedSlots.Count == 0)
            {
                return "尚未收集关键词。请打开资料详情，点击正文中高亮的关键词。";
            }

            StringBuilder builder = new();
            builder.AppendLine("已收集关键词：");
            foreach (Level2KeywordSlot slot in collectedSlots)
            {
                builder.AppendLine($"• {slot.Keyword}");
            }

            return builder.ToString().TrimEnd();
        }

        private List<Level2KeywordSlot> GetSlotsForPanel(int panelIndex)
        {
            List<Level2KeywordSlot> slots = new();
            if (currentChain == null)
            {
                return slots;
            }

            foreach (Level2KeywordSlot slot in currentChain.KeywordSlots)
            {
                if (slot != null && slot.SourcePanelIndex == panelIndex)
                {
                    slots.Add(slot);
                }
            }

            return slots;
        }

        private Level2KeywordSlot FindSlot(string slotId)
        {
            if (currentChain == null || string.IsNullOrWhiteSpace(slotId))
            {
                return null;
            }

            foreach (Level2KeywordSlot slot in currentChain.KeywordSlots)
            {
                if (slot != null &&
                    string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))
                {
                    return slot;
                }
            }

            return null;
        }

        private List<AnswerSlotInfo> GetAnswerSlots()
        {
            List<AnswerSlotInfo> answerSlots = new();
            HashSet<string> seenSlotIds = new();
            if (currentChain == null)
            {
                return answerSlots;
            }

            foreach (Level2KeywordSlot keywordSlot in currentChain.KeywordSlots)
            {
                if (keywordSlot == null ||
                    string.IsNullOrWhiteSpace(keywordSlot.AnswerSlotId) ||
                    !seenSlotIds.Add(keywordSlot.AnswerSlotId))
                {
                    continue;
                }

                answerSlots.Add(
                    new AnswerSlotInfo(
                        keywordSlot.AnswerSlotId,
                        keywordSlot.SlotLabel,
                        GetAnswerSlotSortOrder(keywordSlot.AnswerSlotId)));
            }

            answerSlots.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));
            return answerSlots;
        }

        private static int GetAnswerSlotSortOrder(string answerSlotId)
        {
            if (string.IsNullOrWhiteSpace(answerSlotId))
            {
                return int.MaxValue;
            }

            int startIndex = answerSlotId.Length - 1;
            while (startIndex >= 0 && char.IsDigit(answerSlotId[startIndex]))
            {
                startIndex--;
            }

            string numberText = answerSlotId[(startIndex + 1)..];
            return int.TryParse(numberText, out int number)
                ? number
                : int.MaxValue;
        }

        private void EnsureRuntimeUi()
        {
            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            }

            if (canvas == null)
            {
                return;
            }

            if (font == null)
            {
                TMP_Text existingText =
                    canvas.GetComponentInChildren<TMP_Text>(true);
                if (existingText != null)
                {
                    font = existingText.font;
                }
            }

            if (statusPanel == null)
            {
                CreateStatusPanel(canvas.transform as RectTransform);
            }

            if (analysisOverlay == null)
            {
                CreateAnalysisOverlay(canvas.transform as RectTransform);
            }
        }

        private void CreateStatusPanel(RectTransform parent)
        {
            RectTransform panel = CreateImage(
                "Level2KeywordStatus",
                parent,
                PanelDark,
                new Vector2(0.72f, 0.56f),
                new Vector2(0.98f, 0.88f),
                Vector2.zero,
                Vector2.zero);
            statusPanel = panel.gameObject;
            statusText = CreateText(
                "Text",
                panel,
                "尚未收集关键词。",
                22f,
                FontStyles.Bold,
                Color.white,
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(20f, 18f),
                new Vector2(-20f, -18f));
            SetVisible(statusPanel, false);
        }

        private void CreateAnalysisOverlay(RectTransform parent)
        {
            RectTransform overlay = CreateImage(
                "Level2KeywordAnalysisOverlay",
                parent,
                OverlayColor,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            analysisOverlay = overlay.gameObject;

            RectTransform panel = CreateImage(
                "AnalysisPanel",
                overlay,
                PanelColor,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(980f, 660f),
                Vector2.zero);
            panel.gameObject.AddComponent<Outline>().effectColor = Color.white;

            CreateText(
                "Title",
                panel,
                "证据链诊断",
                34f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0.90f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);

            CreateText(
                "Prompt",
                panel,
                "从右侧选择关键词，填入左侧 6 个槽位，再进行 AI分析。",
                21f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.83f),
                new Vector2(0.94f, 0.90f),
                Vector2.zero,
                Vector2.zero);

            RectTransform slotBox = CreateImage(
                "SlotBox",
                panel,
                Color.white,
                new Vector2(0.05f, 0.27f),
                new Vector2(0.53f, 0.80f),
                Vector2.zero,
                Vector2.zero);
            slotListRoot = CreateVerticalList(slotBox, 12, 12, 12, 12, 8f);

            RectTransform keywordBox = CreateImage(
                "KeywordBox",
                panel,
                Color.white,
                new Vector2(0.57f, 0.27f),
                new Vector2(0.95f, 0.80f),
                Vector2.zero,
                Vector2.zero);
            keywordListRoot = CreateScrollableVerticalList(
                keywordBox,
                out keywordScrollRect,
                12,
                12,
                12,
                12,
                8f);

            analysisText = CreateText(
                "AnalysisText",
                panel,
                string.Empty,
                21f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.TopLeft,
                new Vector2(0.05f, 0.09f),
                new Vector2(0.95f, 0.24f),
                new Vector2(16f, 10f),
                new Vector2(-16f, -10f));

            analysisButton = CreateButton(
                "AnalyzeButton",
                panel,
                "AI分析",
                AccentGreen,
                new Vector2(0.31f, 0.045f),
                new Vector2(220f, 64f));
            analysisButton.onClick.AddListener(RunAnalysis);

            closeButton = CreateButton(
                "CloseButton",
                panel,
                "关闭",
                ButtonBlue,
                new Vector2(0.69f, 0.045f),
                new Vector2(220f, 64f));
            closeButton.onClick.AddListener(CloseAnalysisPanel);

            SetVisible(analysisOverlay, false);
        }

        private RectTransform CreateVerticalList(
            RectTransform parent,
            int left,
            int right,
            int top,
            int bottom,
            float spacing)
        {
            RectTransform root = CreateImage(
                "List",
                parent,
                new Color(0f, 0f, 0f, 0f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return root;
        }

        private RectTransform CreateScrollableVerticalList(
            RectTransform parent,
            out ScrollRect scrollRect,
            int left,
            int right,
            int top,
            int bottom,
            float spacing)
        {
            if (parent.GetComponent<RectMask2D>() == null)
            {
                parent.gameObject.AddComponent<RectMask2D>();
            }

            RectTransform content = CreateRect(
                "Content",
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = parent.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = parent;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;
            scrollRect.inertia = true;

            return content;
        }

        private void RebuildAnalysisPanel()
        {
            float keywordScrollPosition = keywordScrollRect != null
                ? keywordScrollRect.verticalNormalizedPosition
                : 1f;
            ClearButtonList(answerSlotButtons);
            ClearButtonList(collectedKeywordButtons);

            if (currentChain == null)
            {
                return;
            }

            foreach (AnswerSlotInfo slot in GetAnswerSlots())
            {
                string assignedText = string.Empty;
                if (slotAssignments.TryGetValue(slot.SlotId, out string assignedSlotId))
                {
                    Level2KeywordSlot assignedSlot = FindSlot(assignedSlotId);
                    assignedText = assignedSlot != null ? assignedSlot.Keyword : string.Empty;
                }

                Button slotButton = CreateButton(
                    $"Slot_{slot.SlotId}",
                    slotListRoot,
                    string.IsNullOrWhiteSpace(assignedText)
                        ? $"{slot.SlotLabel}:  ______"
                        : $"{slot.SlotLabel}:  {assignedText}",
                    string.IsNullOrWhiteSpace(assignedText) ? ButtonGray : AccentYellow,
                    string.IsNullOrWhiteSpace(assignedText) ? Color.white : TextColor,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 46f));
                ConfigureListButton(slotButton);
                slotButton.onClick.AddListener(() => AssignSelectedKeywordToSlot(slot.SlotId));
                answerSlotButtons.Add(slotButton);
            }

            if (collectedSlots.Count == 0)
            {
                Button emptyHintButton = CreateButton(
                    "KeywordEmptyHint",
                    keywordListRoot,
                    "\u6682\u65e0\u5df2\u6536\u96c6\u5173\u952e\u8bcd",
                    ListItemColor,
                    MutedTextColor,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 46f));
                ConfigureListButton(emptyHintButton);
                emptyHintButton.interactable = false;
                collectedKeywordButtons.Add(emptyHintButton);
            }

            foreach (Level2KeywordSlot slot in collectedSlots)
            {
                bool isSelected =
                    string.Equals(selectedKeywordSlotId, slot.SlotId, StringComparison.Ordinal);
                Button keywordButton = CreateButton(
                    $"Keyword_{slot.SlotId}",
                    keywordListRoot,
                    slot.Keyword,
                    isSelected ? AccentGreen : ListItemColor,
                    isSelected ? Color.white : TextColor,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 46f));
                ConfigureListButton(keywordButton);
                keywordButton.onClick.AddListener(() => SelectCollectedKeyword(slot.SlotId));
                collectedKeywordButtons.Add(keywordButton);
            }

            Canvas.ForceUpdateCanvases();
            if (keywordScrollRect != null)
            {
                keywordScrollRect.verticalNormalizedPosition = keywordScrollPosition;
            }
        }

        private void ConfigureListButton(Button button)
        {
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 46f;
            layout.minHeight = 46f;
            layout.flexibleWidth = 1f;

            Outline outline = button.gameObject.GetComponent<Outline>() ??
                button.gameObject.AddComponent<Outline>();
            outline.effectColor = ListItemBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void ClearButtonList(List<Button> buttons)
        {
            foreach (Button button in buttons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            buttons.Clear();
        }

        private readonly struct AnswerSlotInfo
        {
            public readonly string SlotId;
            public readonly string SlotLabel;
            public readonly int SortOrder;

            public AnswerSlotInfo(
                string slotId,
                string slotLabel,
                int sortOrder)
            {
                SlotId = slotId;
                SlotLabel = slotLabel;
                SortOrder = sortOrder;
            }
        }

        private Button CreateButton(
            string name,
            RectTransform parent,
            string label,
            Color color,
            Vector2 anchor,
            Vector2 size)
        {
            return CreateButton(name, parent, label, color, Color.white, anchor, size);
        }

        private Button CreateButton(
            string name,
            RectTransform parent,
            string label,
            Color color,
            Color labelColor,
            Vector2 anchor,
            Vector2 size)
        {
            RectTransform rect = CreateImage(name, parent, color, anchor, anchor, size, Vector2.zero);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            CreateText(
                "Label",
                rect,
                label,
                23f,
                FontStyles.Bold,
                labelColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                new Vector2(8f, 0f),
                new Vector2(-8f, 0f));
            return button;
        }

        private RectTransform CreateImage(
            string name,
            RectTransform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        private RectTransform CreateRect(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private TMP_Text CreateText(
            string name,
            RectTransform parent,
            string content,
            float size,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void SetVisible(GameObject target, bool isVisible)
        {
            if (target != null)
            {
                target.SetActive(isVisible);
            }
        }
    }
}
