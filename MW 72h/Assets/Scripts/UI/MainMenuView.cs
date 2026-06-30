using System;
using MingBay.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MingBay.UI
{
    /// <summary>
    /// 主菜单界面的交互控制器。
    /// 只负责响应“开始游戏”和“退出游戏”，不承载具体游戏流程逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("明湾/UI/主菜单界面")]
    public sealed class MainMenuView : MonoBehaviour
    {
        private const string BackgroundVolumeKey = "MingBay.Settings.BackgroundVolume";
        private const string SoundEffectsVolumeKey = "MingBay.Settings.SoundEffectsVolume";

        [Header("按钮绑定")]
        [SerializeField]
        [InspectorName("开始游戏按钮")]
        [Tooltip("玩家点击后进入正式游戏场景。标题界面中通常绑定桌面上的“工单APP”。")]
        private Button startButton;

        [SerializeField]
        [InspectorName("附加开始按钮")]
        [Tooltip("可选。用于绑定任务栏“工单队列”等同样进入游戏的入口。")]
        private Button[] additionalStartButtons;

        [SerializeField]
        [InspectorName("设置按钮")]
        [Tooltip("可选。为空时会按 Btn_Settings 名称自动查找。")]
        private Button settingsButton;

        [SerializeField]
        [InspectorName("语言切换占位按钮")]
        [Tooltip("可选。为空时会在主菜单右上角自动生成一个小方形占位按钮。")]
        private Button languageButton;

        [SerializeField]
        [InspectorName("开发人员名单按钮")]
        [Tooltip("可选。为空时会按 Btn_Developers 名称自动查找。")]
        private Button developersButton;

        [SerializeField]
        [InspectorName("退出游戏按钮")]
        [Tooltip("可选。玩家点击后退出游戏；在 Unity 编辑器中测试时会停止播放模式。")]
        private Button exitButton;

        [SerializeField]
        [InspectorName("实时时间文本")]
        [Tooltip("可选。绑定任务栏右侧时间文本后，会按系统时间自动刷新。")]
        private TMP_Text clockText;

        [Header("场景配置")]
        [SerializeField]
        [InspectorName("游戏场景名称")]
        [Tooltip("点击开始按钮后加载的场景名称。必须与 Build Settings 中的场景名称完全一致，策划通常无需修改。")]
        private string gameSceneName = "Level1Scene";

        // 防止玩家连续点击开始按钮，造成同一场景被重复加载。
        private bool isLoading;
        private float nextClockRefreshTime;
        private GameObject settingsOverlay;
        private Slider backgroundVolumeSlider;
        private Slider soundEffectsSlider;
        private TMP_Text languageButtonText;
        private GameObject creditsOverlay;

        private void Awake()
        {
            EnsureRuntimeReferences();
            EnsureLanguageButton();
            EnsureSettingsPanel();
            EnsureCreditsPanel();
        }

        /// <summary>
        /// 组件启用时注册按钮事件。
        /// </summary>
        private void OnEnable()
        {
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            startButton.onClick.AddListener(StartGame);
            AddStartListeners(additionalStartButtons);

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OpenSettingsPanel);
            }

            if (languageButton != null)
            {
                languageButton.onClick.AddListener(ToggleLanguagePlaceholder);
            }

            if (developersButton != null)
            {
                developersButton.onClick.AddListener(OpenCreditsPanel);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(ExitGame);
            }

            RefreshClockText();
        }

        private void Update()
        {
            if (settingsOverlay != null &&
                settingsOverlay.activeSelf &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                CloseSettingsPanel(false);
                return;
            }

            if (creditsOverlay != null &&
                creditsOverlay.activeSelf &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                CloseCreditsPanel();
                return;
            }

            if (clockText != null && Time.unscaledTime >= nextClockRefreshTime)
            {
                RefreshClockText();
            }
        }

        /// <summary>
        /// 组件停用或场景卸载时移除事件，避免重复监听。
        /// </summary>
        private void OnDisable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(StartGame);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(ExitGame);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OpenSettingsPanel);
            }

            if (languageButton != null)
            {
                languageButton.onClick.RemoveListener(ToggleLanguagePlaceholder);
            }

            if (developersButton != null)
            {
                developersButton.onClick.RemoveListener(OpenCreditsPanel);
            }

            RemoveStartListeners(additionalStartButtons);
        }

        /// <summary>
        /// 检查策划在 Inspector 中需要绑定的对象是否完整。
        /// </summary>
        private bool HasRequiredReferences()
        {
            bool isValid = true;

            if (startButton == null)
            {
                Debug.LogError("主菜单缺少“开始游戏按钮”引用，请在 MainMenuView 的 Inspector 中完成绑定。", this);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(gameSceneName))
            {
                Debug.LogError("主菜单的“游戏场景名称”不能为空。", this);
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 锁定按钮并异步进入游戏场景。
        /// </summary>
        private void StartGame()
        {
            if (isLoading)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError(
                    $"无法加载场景“{gameSceneName}”。请确认场景已加入 Build Settings，且名称填写正确。",
                    this);
                return;
            }

            isLoading = true;
            GameRunState.ResetRun();

            // 加载期间关闭按钮，给玩家明确反馈并避免重复操作。
            startButton.interactable = false;
            SetButtonsInteractable(additionalStartButtons, false);
            SetButtonInteractable(settingsButton, false);
            SetButtonInteractable(languageButton, false);
            SetButtonInteractable(developersButton, false);
            if (exitButton != null)
            {
                exitButton.interactable = false;
            }

            SceneManager.LoadSceneAsync(gameSceneName);
        }

        private void EnsureRuntimeReferences()
        {
            if (settingsButton == null)
            {
                settingsButton = FindButton("Btn_Settings");
            }

            if (languageButton == null)
            {
                languageButton = FindButton("Btn_LanguageToggle");
            }

            if (developersButton == null)
            {
                developersButton = FindButton("Btn_Developers");
            }
        }

        private void OpenSettingsPanel()
        {
            EnsureSettingsPanel();
            if (settingsOverlay == null)
            {
                return;
            }

            backgroundVolumeSlider.SetValueWithoutNotify(
                PlayerPrefs.GetFloat(BackgroundVolumeKey, 0.75f));
            soundEffectsSlider.SetValueWithoutNotify(
                PlayerPrefs.GetFloat(SoundEffectsVolumeKey, 0.50f));
            CloseCreditsPanel();
            settingsOverlay.SetActive(true);
            settingsOverlay.transform.SetAsLastSibling();
        }

        private void CloseSettingsPanel(bool applyChanges)
        {
            if (applyChanges)
            {
                PlayerPrefs.SetFloat(BackgroundVolumeKey, backgroundVolumeSlider.value);
                PlayerPrefs.SetFloat(SoundEffectsVolumeKey, soundEffectsSlider.value);
                PlayerPrefs.Save();
            }

            if (settingsOverlay != null)
            {
                settingsOverlay.SetActive(false);
            }
        }

        private void OpenCreditsPanel()
        {
            EnsureCreditsPanel();
            if (creditsOverlay == null)
            {
                return;
            }

            CloseSettingsPanel(false);
            creditsOverlay.SetActive(true);
            creditsOverlay.transform.SetAsLastSibling();
        }

        private void CloseCreditsPanel()
        {
            if (creditsOverlay != null)
            {
                creditsOverlay.SetActive(false);
            }
        }

        private void ToggleLanguagePlaceholder()
        {
            if (languageButtonText == null && languageButton != null)
            {
                languageButtonText = languageButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (languageButtonText == null)
            {
                return;
            }

            languageButtonText.text =
                string.Equals(languageButtonText.text, "中", StringComparison.Ordinal)
                    ? "EN"
                    : "中";
        }

        private void EnsureLanguageButton()
        {
            if (languageButton != null)
            {
                languageButtonText = languageButton.GetComponentInChildren<TMP_Text>(true);
                return;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                return;
            }

            TMP_FontAsset font = FindSceneFont(canvas);
            RectTransform rect = CreateRect(
                "Btn_LanguageToggle",
                canvas.transform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-72f, -72f),
                new Vector2(56f, 56f));
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.78f, 0.78f, 0.78f, 0.18f);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.92f, 0.92f, 0.92f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            languageButton = rect.gameObject.AddComponent<Button>();
            languageButton.targetGraphic = image;
            ColorBlock colors = languageButton.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.10f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.22f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.32f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            languageButton.colors = colors;

            languageButtonText = CreateText(
                "Txt_LanguageToggle",
                rect,
                font,
                "中",
                22f,
                FontStyles.Bold,
                Color.white,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
        }

        private void EnsureSettingsPanel()
        {
            if (settingsOverlay != null)
            {
                return;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                return;
            }

            TMP_FontAsset font = FindSceneFont(canvas);
            RectTransform overlay = CreateRect(
                "SettingsOverlay",
                canvas.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            settingsOverlay = overlay.gameObject;

            Image overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.72f);
            overlayImage.raycastTarget = true;

            RectTransform panel = CreateRect(
                "SettingsPanel",
                overlay,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(660f, 410f));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.56f, 0.56f, 0.56f, 1f);
            Outline panelOutline = panel.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            panelOutline.effectDistance = new Vector2(4f, -4f);

            CreateText(
                "Txt_SettingsTitle",
                panel,
                font,
                "设置",
                28f,
                FontStyles.Bold,
                new Color(0.28f, 0.28f, 0.28f, 1f),
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -40f),
                new Vector2(240f, 44f));

            CreateButton(
                "Btn_SettingsClose",
                panel,
                font,
                "×",
                30f,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -28f),
                new Vector2(36f, 36f),
                new Color(1f, 1f, 1f, 0f),
                new Color(0.94f, 0.94f, 0.94f, 1f),
                () => CloseSettingsPanel(false));

            CreateText(
                "Txt_BackgroundVolume",
                panel,
                font,
                "背景音量",
                24f,
                FontStyles.Bold,
                new Color(0.35f, 0.35f, 0.35f, 1f),
                TextAlignmentOptions.MidlineRight,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-180f, 78f),
                new Vector2(160f, 40f));
            backgroundVolumeSlider = CreateSlider(
                "Slider_BackgroundVolume",
                panel,
                new Vector2(110f, 78f),
                0.75f);

            CreateText(
                "Txt_SoundEffects",
                panel,
                font,
                "音效",
                24f,
                FontStyles.Bold,
                new Color(0.35f, 0.35f, 0.35f, 1f),
                TextAlignmentOptions.MidlineRight,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-180f, 4f),
                new Vector2(160f, 40f));
            soundEffectsSlider = CreateSlider(
                "Slider_SoundEffects",
                panel,
                new Vector2(110f, 4f),
                0.50f);

            CreateButton(
                "Btn_SettingsCancel",
                panel,
                font,
                "取消",
                24f,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-165f, -126f),
                new Vector2(260f, 82f),
                new Color(0.76f, 0.76f, 0.76f, 1f),
                new Color(0.23f, 0.23f, 0.23f, 1f),
                () => CloseSettingsPanel(false));
            CreateButton(
                "Btn_SettingsConfirm",
                panel,
                font,
                "确认",
                24f,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(165f, -126f),
                new Vector2(260f, 82f),
                new Color(0.76f, 0.76f, 0.76f, 1f),
                new Color(0.23f, 0.23f, 0.23f, 1f),
                () => CloseSettingsPanel(true));

            settingsOverlay.SetActive(false);
        }

        private void EnsureCreditsPanel()
        {
            if (creditsOverlay != null)
            {
                return;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                return;
            }

            TMP_FontAsset font = FindSceneFont(canvas);
            RectTransform overlay = CreateRect(
                "CreditsOverlay",
                canvas.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            creditsOverlay = overlay.gameObject;

            Image overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.76f);
            overlayImage.raycastTarget = true;

            Button closeButton = overlay.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = overlayImage;
            ColorBlock colors = closeButton.colors;
            colors.normalColor = overlayImage.color;
            colors.highlightedColor = overlayImage.color;
            colors.pressedColor = new Color(0f, 0f, 0f, 0.82f);
            colors.selectedColor = overlayImage.color;
            colors.fadeDuration = 0.08f;
            closeButton.colors = colors;
            closeButton.onClick.AddListener(CloseCreditsPanel);

            RectTransform listRoot = CreateRect(
                "CreditsList",
                overlay,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 18f),
                new Vector2(480f, 180f));

            CreateCreditRow(
                listRoot,
                font,
                "Planning",
                "策划：",
                "圈圈",
                54f);
            CreateCreditRow(
                listRoot,
                font,
                "Programming",
                "程序：",
                "老泉、萱条",
                18f);
            CreateCreditRow(
                listRoot,
                font,
                "Art",
                "美术：",
                "番茄大王、明宇",
                -18f);
            CreateCreditRow(
                listRoot,
                font,
                "Sound",
                "音效：",
                "我要反派角色",
                -54f);

            CreateText(
                "Txt_CreditsCloseHint",
                overlay,
                font,
                "点击任意位置关闭",
                22f,
                FontStyles.Normal,
                new Color(0.72f, 0.72f, 0.72f, 1f),
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -260f),
                new Vector2(340f, 40f));

            creditsOverlay.SetActive(false);
        }

        private static void CreateCreditRow(
            RectTransform parent,
            TMP_FontAsset font,
            string localizationKey,
            string role,
            string names,
            float y)
        {
            RectTransform row = CreateRect(
                $"CreditsRow_{localizationKey}",
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, y),
                new Vector2(480f, 32f));

            CreateText(
                $"Txt_CreditsRole_{localizationKey}",
                row,
                font,
                role,
                28f,
                FontStyles.Bold,
                Color.white,
                TextAlignmentOptions.MidlineRight,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(190f, 0f),
                new Vector2(150f, 32f));

            CreateText(
                $"Txt_CreditsNames_{localizationKey}",
                row,
                font,
                names,
                28f,
                FontStyles.Bold,
                Color.white,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(390f, 0f),
                new Vector2(250f, 32f));
        }

        private static Slider CreateSlider(
            string name,
            RectTransform parent,
            Vector2 anchoredPosition,
            float value)
        {
            RectTransform sliderRect = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                new Vector2(360f, 58f));

            RectTransform track = CreateImage(
                "Track",
                sliderRect,
                new Color(0.42f, 0.42f, 0.42f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(0f, 16f));
            RectTransform fill = CreateImage(
                "Fill",
                track,
                new Color(0.83f, 0.83f, 0.83f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RectTransform handle = CreateImage(
                "Handle",
                sliderRect,
                new Color(0.86f, 0.86f, 0.86f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(34f, 64f));

            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
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
            Vector2 sizeDelta,
            Color backgroundColor,
            Color textColor,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition,
                sizeDelta);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = backgroundColor;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = new Color(
                Mathf.Min(backgroundColor.r + 0.08f, 1f),
                Mathf.Min(backgroundColor.g + 0.08f, 1f),
                Mathf.Min(backgroundColor.b + 0.08f, 1f),
                Mathf.Max(backgroundColor.a, 0.12f));
            colors.pressedColor = new Color(
                Mathf.Max(backgroundColor.r - 0.08f, 0f),
                Mathf.Max(backgroundColor.g - 0.08f, 0f),
                Mathf.Max(backgroundColor.b - 0.08f, 0f),
                Mathf.Max(backgroundColor.a, 0.18f));
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            CreateText(
                $"Txt_{name}",
                rect,
                font,
                label,
                fontSize,
                FontStyles.Bold,
                textColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        private void AddStartListeners(Button[] buttons)
        {
            if (buttons == null)
            {
                return;
            }

            foreach (Button button in buttons)
            {
                if (button != null)
                {
                    button.onClick.AddListener(StartGame);
                }
            }
        }

        private void RemoveStartListeners(Button[] buttons)
        {
            if (buttons == null)
            {
                return;
            }

            foreach (Button button in buttons)
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(StartGame);
                }
            }
        }

        private static void SetButtonsInteractable(Button[] buttons, bool interactable)
        {
            if (buttons == null)
            {
                return;
            }

            foreach (Button button in buttons)
            {
                SetButtonInteractable(button, interactable);
            }
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void RefreshClockText()
        {
            if (clockText == null)
            {
                return;
            }

            DateTime now = DateTime.Now;
            string period = now.Hour < 12 ? "上午" : "下午";
            int hour = now.Hour % 12;
            if (hour == 0)
            {
                hour = 12;
            }

            clockText.text = $"{period}{hour}:{now.Minute:00}";
            nextClockRefreshTime = Time.unscaledTime + 1f;
        }

        private static Button FindButton(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            return target != null ? target.GetComponent<Button>() : null;
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
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition,
                sizeDelta);
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
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        /// <summary>
        /// 正式包中退出应用；编辑器中停止播放，方便策划测试。
        /// </summary>
        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
