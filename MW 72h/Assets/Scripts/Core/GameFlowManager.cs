using MingBay.Data;
using MingBay.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MingBay.Core
{
    /// <summary>
    /// 最小 Demo 的流程总控。
    /// 负责读取工单、切换状态、记录选择，并通知 UI 刷新。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("明湾/核心/游戏流程管理器")]
    public sealed class GameFlowManager : MonoBehaviour
    {
        [Header("数据配置")]
        [SerializeField]
        [InspectorName("Demo 数据库")]
        [Tooltip("包含本次 Demo 工单顺序的数据资产。策划调整工单时修改该资产即可。")]
        private DemoDatabase database;

        [Header("场景对象")]
        [SerializeField]
        [InspectorName("证据管理器")]
        [Tooltip("负责记录证据数量并阻止重复保存。应绑定同场景中的 EvidenceManager。")]
        private EvidenceManager evidenceManager;

        [SerializeField]
        [InspectorName("游戏主界面")]
        [Tooltip("负责显示工单、资料、按钮和结果的 MainGameView。")]
        private MainGameView mainGameView;

        [Header("场景配置")]
        [SerializeField]
        [InspectorName("主菜单场景名称")]
        [Tooltip("点击结果面板中的“返回主菜单”后加载的场景。必须存在于 Build Settings。")]
        private string titleSceneName = "TitleScene";

        private GameState currentState;
        private TicketData currentTicket;
        private int currentTicketIndex;
        private int resolvedCount;
        private bool isSceneLoading;

        private void OnEnable()
        {
            if (mainGameView == null)
            {
                return;
            }

            mainGameView.ViewDataRequested += HandleViewDataRequested;
            mainGameView.SaveEvidenceRequested += HandleSaveEvidenceRequested;
            mainGameView.MarkResolvedRequested += HandleMarkResolvedRequested;
            mainGameView.ReturnToTitleRequested += HandleReturnToTitleRequested;
        }

        private void OnDisable()
        {
            if (mainGameView == null)
            {
                return;
            }

            mainGameView.ViewDataRequested -= HandleViewDataRequested;
            mainGameView.SaveEvidenceRequested -= HandleSaveEvidenceRequested;
            mainGameView.MarkResolvedRequested -= HandleMarkResolvedRequested;
            mainGameView.ReturnToTitleRequested -= HandleReturnToTitleRequested;
        }

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            StartGame();
        }

        /// <summary>
        /// 重置运行时数据并显示第一条工单。
        /// </summary>
        private void StartGame()
        {
            evidenceManager.ResetEvidence();
            resolvedCount = 0;
            currentTicketIndex = 0;
            currentTicket = database.GetTicket(currentTicketIndex);

            if (currentTicket == null)
            {
                Debug.LogError("Demo 数据库中没有可用工单，请至少配置一条工单。", database);
                return;
            }

            currentState = GameState.ReadingTicket;
            mainGameView.ShowTicket(
                currentTicket,
                currentTicketIndex + 1,
                database.TicketCount,
                evidenceManager.EvidenceCount,
                resolvedCount);
        }

        /// <summary>
        /// 玩家查看资料后，解锁两种最终处置按钮。
        /// </summary>
        private void HandleViewDataRequested()
        {
            if (currentState != GameState.ReadingTicket)
            {
                return;
            }

            currentState = GameState.ReviewingData;
            mainGameView.ShowData(currentTicket);
        }

        /// <summary>
        /// 保存当前工单证据并进入结果状态。
        /// </summary>
        private void HandleSaveEvidenceRequested()
        {
            if (currentState != GameState.ReviewingData)
            {
                return;
            }

            bool saved = currentTicket.HasEvidence &&
                         evidenceManager.SaveEvidence(currentTicket.EvidenceId);

            string resultTitle = saved ? "证据已保留" : "未发现有效证据";
            string resultText = saved
                ? currentTicket.OnSaveEvidenceText
                : "当前工单没有可保存的新证据。该选择不会增加证据数量。";

            FinishTicket(resultTitle, resultText, saved);
        }

        /// <summary>
        /// 将工单标记为已解决，并记录已解决数量。
        /// </summary>
        private void HandleMarkResolvedRequested()
        {
            if (currentState != GameState.ReviewingData)
            {
                return;
            }

            resolvedCount++;
            FinishTicket("工单已关闭", currentTicket.OnResolvedText, false);
        }

        /// <summary>
        /// 单工单纵向切片在给出结果后结束。
        /// 后续扩展四条工单时，可在这里改为推进下一条工单。
        /// </summary>
        private void FinishTicket(string resultTitle, string resultText, bool evidenceSaved)
        {
            currentState = GameState.ShowingResult;
            mainGameView.ShowResult(
                resultTitle,
                resultText,
                evidenceSaved,
                evidenceManager.EvidenceCount,
                resolvedCount);
            currentState = GameState.Finished;
        }

        /// <summary>
        /// 从结果界面返回主菜单。
        /// </summary>
        private void HandleReturnToTitleRequested()
        {
            if (isSceneLoading)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
            {
                Debug.LogError($"无法加载场景“{titleSceneName}”，请检查 Build Settings。", this);
                return;
            }

            isSceneLoading = true;
            SceneManager.LoadSceneAsync(titleSceneName);
        }

        /// <summary>
        /// 检查场景中必须绑定的引用，并输出策划可理解的中文错误。
        /// </summary>
        private bool ValidateReferences()
        {
            bool isValid = true;

            if (database == null)
            {
                Debug.LogError("GameFlowManager 缺少“Demo 数据库”引用。", this);
                isValid = false;
            }

            if (evidenceManager == null)
            {
                Debug.LogError("GameFlowManager 缺少“证据管理器”引用。", this);
                isValid = false;
            }

            if (mainGameView == null)
            {
                Debug.LogError("GameFlowManager 缺少“游戏主界面”引用。", this);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(titleSceneName))
            {
                Debug.LogError("GameFlowManager 的“主菜单场景名称”不能为空。", this);
                isValid = false;
            }

            return isValid;
        }
    }
}
