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
        [Header("按钮绑定")]
        [SerializeField]
        [InspectorName("开始游戏按钮")]
        [Tooltip("玩家点击后进入正式游戏场景。请绑定主菜单中的“开始值班”按钮。")]
        private Button startButton;

        [SerializeField]
        [InspectorName("退出游戏按钮")]
        [Tooltip("玩家点击后退出游戏；在 Unity 编辑器中测试时会停止播放模式。")]
        private Button exitButton;

        [Header("场景配置")]
        [SerializeField]
        [InspectorName("游戏场景名称")]
        [Tooltip("点击开始按钮后加载的场景名称。必须与 Build Settings 中的场景名称完全一致，策划通常无需修改。")]
        private string gameSceneName = "GameScene";

        // 防止玩家连续点击开始按钮，造成同一场景被重复加载。
        private bool isLoading;

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
            exitButton.onClick.AddListener(ExitGame);
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

            if (exitButton == null)
            {
                Debug.LogError("主菜单缺少“退出游戏按钮”引用，请在 MainMenuView 的 Inspector 中完成绑定。", this);
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

            // 加载期间关闭按钮，给玩家明确反馈并避免重复操作。
            startButton.interactable = false;
            exitButton.interactable = false;
            SceneManager.LoadSceneAsync(gameSceneName);
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
