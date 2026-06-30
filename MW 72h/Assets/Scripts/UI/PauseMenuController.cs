using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MingBay.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MingBay/UI/Pause Menu Controller")]
    public sealed class PauseMenuController : MonoBehaviour
    {
        private const string SaveSceneKey = "MingBay.Save.SceneName";
        private const string SaveTimeKey = "MingBay.Save.Time";
        private const string TitleSceneName = "TitleScene";

        private static readonly Color BackgroundColor = Hex("111111");
        private static readonly Color TopEdgeColor = Hex("1B1B1B");
        private static readonly Color PrimaryTextColor = Hex("F1F1F1");
        private static readonly Color MutedTextColor = Hex("B8B8B8");

        private GameObject menuRoot;
        private TMP_Text statusText;
        private Button restartButton;
        private Button saveButton;
        private Button loadButton;
        private Button titleButton;
        private Button resumeButton;
        private float previousTimeScale = 1f;
        private bool isOpen;
        private bool isLoadingScene;

        private void Awake()
        {
            EnsureMenu();
            SetMenuVisible(false);
        }

        private void Update()
        {
            if (isLoadingScene || !Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (isOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        private void OnDisable()
        {
            if (isOpen)
            {
                Time.timeScale = 1f;
            }
        }

        private void EnsureMenu()
        {
            if (menuRoot != null)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            }

            if (canvas == null)
            {
                Debug.LogError("PauseMenuController cannot find a Canvas.", this);
                enabled = false;
                return;
            }

            TMP_FontAsset font = FindSceneFont(canvas);
            RectTransform root = CreateRect(
                "PauseMenuOverlay",
                canvas.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            menuRoot = root.gameObject;

            Image background = menuRoot.AddComponent<Image>();
            background.color = new Color(
                BackgroundColor.r,
                BackgroundColor.g,
                BackgroundColor.b,
                0.96f);
            background.raycastTarget = true;

            CreateImage(
                "TopEdge",
                root,
                TopEdgeColor,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, -4f),
                new Vector2(0f, 8f));

            RectTransform menu = CreateRect(
                "PauseMenu",
                root,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(136f, -100f),
                new Vector2(700f, 520f));
            menu.pivot = new Vector2(0f, 0.5f);

            CreateText(
                "Txt_PauseTitle",
                menu,
                font,
                "暂停",
                72f,
                FontStyles.Bold,
                PrimaryTextColor,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, -92f),
                new Vector2(640f, 100f));

            restartButton = CreateTextMenuButton(
                "Btn_RestartLevel",
                menu,
                font,
                "重新开始本关",
                42f,
                new Vector2(0f, -158f),
                new Vector2(420f, 56f),
                RestartCurrentLevel);
            saveButton = CreateTextMenuButton(
                "Btn_SaveGame",
                menu,
                font,
                "保存游戏",
                38f,
                new Vector2(0f, -224f),
                new Vector2(300f, 48f),
                SaveGame);
            loadButton = CreateTextMenuButton(
                "Btn_LoadGame",
                menu,
                font,
                "读取游戏",
                38f,
                new Vector2(0f, -278f),
                new Vector2(300f, 48f),
                LoadGame);
            titleButton = CreateTextMenuButton(
                "Btn_ReturnTitle",
                menu,
                font,
                "返回主菜单",
                38f,
                new Vector2(0f, -332f),
                new Vector2(360f, 48f),
                ReturnToTitle);
            resumeButton = CreateTextMenuButton(
                "Btn_Resume",
                menu,
                font,
                "返回",
                38f,
                new Vector2(0f, -386f),
                new Vector2(240f, 48f),
                CloseMenu);

            statusText = CreateText(
                "Txt_PauseStatus",
                menu,
                font,
                string.Empty,
                24f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, -454f),
                new Vector2(640f, 56f));

            menuRoot.SetActive(false);
        }

        private void OpenMenu()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            SetStatus(string.Empty);
            SetMenuVisible(true);

            if (EventSystem.current != null && resumeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            }
        }

        private void CloseMenu()
        {
            Time.timeScale = Mathf.Approximately(previousTimeScale, 0f)
                ? 1f
                : previousTimeScale;
            SetMenuVisible(false);
        }

        private void RestartCurrentLevel()
        {
            LoadScene(SceneManager.GetActiveScene().name);
        }

        private void SaveGame()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.name))
            {
                SetStatus("保存失败：当前场景无效");
                return;
            }

            PlayerPrefs.SetString(SaveSceneKey, scene.name);
            PlayerPrefs.SetString(SaveTimeKey, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            PlayerPrefs.Save();
            SetStatus($"已保存当前关卡：{scene.name}");
        }

        private void LoadGame()
        {
            string savedSceneName = PlayerPrefs.GetString(SaveSceneKey, string.Empty);
            if (string.IsNullOrWhiteSpace(savedSceneName))
            {
                SetStatus("暂无可读取的存档");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(savedSceneName))
            {
                SetStatus($"读取失败：场景 {savedSceneName} 未加入 Build Settings");
                return;
            }

            LoadScene(savedSceneName);
        }

        private void ReturnToTitle()
        {
            if (!Application.CanStreamedLevelBeLoaded(TitleSceneName))
            {
                SetStatus("返回失败：TitleScene 未加入 Build Settings");
                return;
            }

            LoadScene(TitleSceneName);
        }

        private void LoadScene(string sceneName)
        {
            if (isLoadingScene || string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            isLoadingScene = true;
            SetButtonsInteractable(false);
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync(sceneName);
        }

        private void SetMenuVisible(bool visible)
        {
            isOpen = visible;
            if (menuRoot != null)
            {
                menuRoot.SetActive(visible);
            }
        }

        private void SetStatus(string text)
        {
            if (statusText != null)
            {
                statusText.text = text ?? string.Empty;
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            SetButtonInteractable(restartButton, interactable);
            SetButtonInteractable(saveButton, interactable);
            SetButtonInteractable(loadButton, interactable);
            SetButtonInteractable(titleButton, interactable);
            SetButtonInteractable(resumeButton, interactable);
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
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

        private static Button CreateTextMenuButton(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            float fontSize,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                anchoredPosition,
                sizeDelta);
            rect.pivot = new Vector2(0f, 0.5f);

            Image target = rect.gameObject.AddComponent<Image>();
            target.color = new Color(1f, 1f, 1f, 0f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.02f);
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
                PrimaryTextColor,
                TextAlignmentOptions.MidlineLeft,
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
            label.enableWordWrapping = false;
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

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString($"#{hex}", out Color color))
            {
                return color;
            }

            return Color.white;
        }
    }
}
