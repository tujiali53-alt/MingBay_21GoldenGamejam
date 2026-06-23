using System.Collections.Generic;
using MingBay.Data;
using MingBay.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MingBay.Core
{
    /// <summary>
    /// 工单 Demo 的流程总控。
    /// 负责推进教程与正式值班阶段、记录选择，并通知 UI 刷新。
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
        [InspectorName("指标管理器")]
        [Tooltip("记录已解决、人工转接、用户满意度与 A-07 风险。旧场景未绑定时会自动创建。")]
        private MetricManager metricManager;

        [SerializeField]
        [InspectorName("游戏主界面")]
        [Tooltip("负责显示工单、资料、按钮和结果的 MainGameView。")]
        private MainGameView mainGameView;

        [Header("阶段配置")]
        [SerializeField]
        [InspectorName("阶段顺序")]
        [Tooltip("按游戏实际推进顺序填写阶段 ID。默认先教程，再进入第一天正式值班。")]
        private string[] stageOrder = { "Stage_Tutorial", "Stage_Day1" };

        [SerializeField]
        [InspectorName("阶段显示名称")]
        [Tooltip("与阶段顺序一一对应，用于顶部状态和阶段完成按钮。")]
        private string[] stageDisplayNames = { "教程关卡", "第一天正式值班" };

        [Header("场景配置")]
        [SerializeField]
        [InspectorName("主菜单场景名称")]
        [Tooltip("点击结果面板中的“返回主菜单”后加载的场景。必须存在于 Build Settings。")]
        private string titleSceneName = "TitleScene";

        private GameState currentState;
        private readonly List<TicketData> currentStageTickets = new();
        private TicketData currentTicket;
        private int currentStageIndex = -1;
        private int currentTicketIndex;
        private int processedCount;
        private bool[] processedTickets;
        private bool isSceneLoading;
        private int selectedEvidenceIndex = -1;
        private int retainedEvidenceIndex = -1;
        private int currentFollowUpIndex;
        private string pendingResultTitle;
        private string pendingResultText;
        private bool pendingEvidenceSaved;
        private bool pendingEvidenceCorrect;

        private void Awake()
        {
            if (metricManager == null)
            {
                metricManager = GetComponent<MetricManager>();
            }

            if (metricManager == null)
            {
                metricManager = gameObject.AddComponent<MetricManager>();
            }
        }

        private void OnEnable()
        {
            if (mainGameView == null)
            {
                return;
            }

            mainGameView.TicketSelected += HandleTicketSelected;
            mainGameView.ViewDataRequested += HandleViewDataRequested;
            mainGameView.FollowUpRequested += HandleFollowUpRequested;
            mainGameView.TransferHumanRequested += HandleTransferHumanRequested;
            mainGameView.SaveEvidenceRequested += HandleSaveEvidenceRequested;
            mainGameView.ChatEvidenceActionRequested += HandleChatEvidenceActionRequested;
            mainGameView.EvidenceSelected += HandleEvidenceSelected;
            mainGameView.MarkResolvedRequested += HandleMarkResolvedRequested;
            mainGameView.ResultActionRequested += HandleResultActionRequested;
        }

        private void OnDisable()
        {
            if (mainGameView == null)
            {
                return;
            }

            mainGameView.TicketSelected -= HandleTicketSelected;
            mainGameView.ViewDataRequested -= HandleViewDataRequested;
            mainGameView.FollowUpRequested -= HandleFollowUpRequested;
            mainGameView.TransferHumanRequested -= HandleTransferHumanRequested;
            mainGameView.SaveEvidenceRequested -= HandleSaveEvidenceRequested;
            mainGameView.ChatEvidenceActionRequested -= HandleChatEvidenceActionRequested;
            mainGameView.EvidenceSelected -= HandleEvidenceSelected;
            mainGameView.MarkResolvedRequested -= HandleMarkResolvedRequested;
            mainGameView.ResultActionRequested -= HandleResultActionRequested;
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
        /// 重置运行时数据并显示可点击的工单队列。
        /// </summary>
        private void StartGame()
        {
            evidenceManager.ResetEvidence();
            metricManager.ResetMetrics();
            currentStageIndex = -1;
            currentTicket = null;

            if (database.TicketCount <= 0)
            {
                Debug.LogError("Demo 数据库中没有可用工单，请至少配置一条工单。", database);
                return;
            }

            if (!LoadNextStage())
            {
                // 兼容尚未执行新版生成工具的旧数据库，避免项目直接无法进入游戏。
                currentStageIndex = 0;
                currentStageTickets.Clear();
                currentStageTickets.AddRange(database.Tickets);
                processedCount = 0;
                currentTicketIndex = -1;
                processedTickets = new bool[currentStageTickets.Count];
                mainGameView.BuildTicketQueue(currentStageTickets);
                ShowTicketSelection();
                Debug.LogWarning(
                    "数据库中没有匹配新版阶段 ID 的工单，已按旧列表进入兼容模式。" +
                    "请执行“明湾/场景工具/生成基础工单 Demo”更新配置。",
                    database);
            }
        }

        /// <summary>
        /// 加载阶段顺序中的下一组有效工单。
        /// </summary>
        private bool LoadNextStage()
        {
            while (++currentStageIndex < stageOrder.Length)
            {
                currentStageTickets.Clear();
                currentStageTickets.AddRange(
                    database.GetTicketsByStage(stageOrder[currentStageIndex]));

                if (currentStageTickets.Count == 0)
                {
                    continue;
                }

                processedCount = 0;
                currentTicketIndex = -1;
                currentTicket = null;
                processedTickets = new bool[currentStageTickets.Count];
                mainGameView.BuildTicketQueue(currentStageTickets);
                ShowTicketSelection();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 显示左侧工单列表，等待玩家主动点击一条未处理工单。
        /// </summary>
        private void ShowTicketSelection()
        {
            currentState = GameState.TicketSelection;
            mainGameView.ShowTicketSelection(
                GetCurrentStageDisplayName(),
                processedCount,
                currentStageTickets.Count,
                GetMetrics(),
                processedTickets);
        }

        /// <summary>
        /// 玩家点击左侧工单后加载对应内容。
        /// 在选择、阅读和查看资料阶段可以切换未处理工单；
        /// 进入证据提交或结算阶段后锁定，避免重复应用指标。
        /// </summary>
        private void HandleTicketSelected(int ticketIndex)
        {
            bool canSwitchTicket =
                currentState == GameState.TicketSelection ||
                currentState == GameState.ReadingTicket ||
                currentState == GameState.ReviewingData;

            if (!canSwitchTicket ||
                ticketIndex < 0 ||
                ticketIndex >= currentStageTickets.Count ||
                processedTickets[ticketIndex])
            {
                return;
            }

            currentTicketIndex = ticketIndex;
            currentTicket = currentStageTickets[ticketIndex];
            if (currentTicket == null)
            {
                Debug.LogError($"工单索引 {ticketIndex} 没有有效配置。", database);
                return;
            }

            selectedEvidenceIndex = -1;
            retainedEvidenceIndex = -1;
            currentFollowUpIndex = 0;
            currentState = GameState.ReadingTicket;
            mainGameView.ShowTicket(
                currentTicket,
                GetCurrentStageDisplayName(),
                currentTicketIndex + 1,
                currentStageTickets.Count,
                GetMetrics());
            mainGameView.RefreshQueueStates(currentTicketIndex, processedTickets);
        }

        /// <summary>
        /// 玩家查看资料后，解锁追问、转人工、教程保留证据和标记已解决。
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
        /// 追问用户；需要核验证据的工单进入证据选择阶段。
        /// </summary>
        private void HandleFollowUpRequested()
        {
            if (currentState != GameState.ReviewingData)
            {
                return;
            }

            string[] followUpLines = currentTicket.FollowUpLines;
            if (currentFollowUpIndex >= followUpLines.Length)
            {
                return;
            }

            if (currentFollowUpIndex == 0)
            {
                metricManager.Apply(currentTicket.FollowUpMetricDelta);
            }

            string followUpLine = followUpLines[currentFollowUpIndex];
            currentFollowUpIndex++;
            bool hasRemainingFollowUp = currentFollowUpIndex < followUpLines.Length;
            mainGameView.ShowResidentFollowUp(followUpLine, hasRemainingFollowUp);

            if (hasRemainingFollowUp)
            {
                return;
            }

            if (currentTicket.TicketId == "T_S01_002")
            {
                mainGameView.ShowResolveTutorialHint();
                return;
            }

            // 教程第一张工单的“追问”与“查看资料 / 保留证据”属于并联操作。
            // 追问后继续停留在资料处理阶段，避免下一次点击资料被误判为直接提交证据。
            if (currentTicket.AllowDirectEvidenceSave)
            {
                return;
            }

            BeginEvidenceOrResolutionHint(string.Empty);
        }

        /// <summary>
        /// 将工单转交人工；需要核验证据的工单进入证据选择阶段。
        /// </summary>
        private void HandleTransferHumanRequested()
        {
            if (currentState != GameState.ReviewingData)
            {
                return;
            }

            if (currentTicket.AllowDirectEvidenceSave &&
                currentTicket.RequiresEvidenceSelection &&
                retainedEvidenceIndex < 0)
            {
                mainGameView.ShowRetainedEvidenceRequired();
                return;
            }

            metricManager.Apply(currentTicket.TransferMetricDelta);

            if (currentTicket.AllowDirectEvidenceSave &&
                currentTicket.RequiresEvidenceSelection)
            {
                currentState = GameState.AwaitingEvidencePresentation;
                mainGameView.ShowEvidencePresentationRequest(
                    currentTicket,
                    currentTicket.TransferText,
                    currentTicket.EvidencePromptText);
                return;
            }

            BeginEvidenceOrResolutionHint(currentTicket.TransferText);
        }

        /// <summary>
        /// 教程中直接保留当前工单的有效证据，但不结束工单。
        /// </summary>
        private void HandleSaveEvidenceRequested()
        {
            if (currentState != GameState.ReviewingData ||
                !currentTicket.AllowDirectEvidenceSave ||
                !currentTicket.HasEvidence ||
                selectedEvidenceIndex < 0)
            {
                return;
            }

            retainedEvidenceIndex = selectedEvidenceIndex;
            mainGameView.ShowDirectEvidenceSaved(
                retainedEvidenceIndex,
                GetMetrics());
        }

        private void BeginEvidenceOrResolutionHint(string actionText)
        {
            if (!currentTicket.RequiresEvidenceSelection)
            {
                mainGameView.ShowResolutionHint(actionText);
                return;
            }

            currentState = GameState.AwaitingEvidence;
            mainGameView.ShowEvidenceSelection(
                currentTicket,
                actionText,
                currentTicket.EvidencePromptText);
        }

        /// <summary>
        /// 核对玩家提交的资料序号，并应用正确或错误证据分支。
        /// </summary>
        private void HandleEvidenceSelected(int evidenceIndex)
        {
            if (currentState == GameState.ReviewingData &&
                currentTicket.AllowDirectEvidenceSave)
            {
                selectedEvidenceIndex = evidenceIndex;
                mainGameView.ShowEvidenceCandidateSelected(evidenceIndex);
                return;
            }

            if (currentState != GameState.AwaitingEvidence)
            {
                return;
            }

            ResolveEvidence(evidenceIndex);
        }

        /// <summary>
        /// 根据提交的资料序号应用正确或错误证据分支。
        /// </summary>
        private void ResolveEvidence(int evidenceIndex)
        {
            bool isCorrect =
                currentTicket.HasEvidence &&
                evidenceIndex == currentTicket.CorrectEvidenceIndex;
            bool evidenceSaved =
                isCorrect &&
                (evidenceIndex == retainedEvidenceIndex ||
                 evidenceManager.SaveEvidence(currentTicket.EvidenceId));

            if (isCorrect)
            {
                metricManager.Apply(currentTicket.CorrectEvidenceMetricDelta);
                if (!currentTicket.FinishOnEvidenceSubmission)
                {
                    currentState = GameState.ReviewingData;
                    mainGameView.ShowTutorialEvidenceAccepted(
                        currentTicket.OnSaveEvidenceText,
                        GetMetrics());
                    return;
                }

                FinishTicket("证据核验正确", currentTicket.OnSaveEvidenceText, evidenceSaved);
                return;
            }

            metricManager.Apply(currentTicket.WrongEvidenceMetricDelta);
            if (!currentTicket.FinishOnEvidenceSubmission)
            {
                mainGameView.ShowTutorialEvidenceRejected(
                    currentTicket.OnWrongEvidenceText,
                    GetMetrics());
                return;
            }

            FinishTicket("证据核验错误", currentTicket.OnWrongEvidenceText, false);
        }

        /// <summary>
        /// 处理聊天区域中的“出示证据”和“完成对话”按钮。
        /// </summary>
        private void HandleChatEvidenceActionRequested()
        {
            if (currentState == GameState.AwaitingEvidencePresentation)
            {
                bool isCorrect =
                    currentTicket.HasEvidence &&
                    retainedEvidenceIndex == currentTicket.CorrectEvidenceIndex;
                pendingEvidenceCorrect = isCorrect;
                pendingEvidenceSaved = false;
                pendingResultTitle = isCorrect ? "证据核验正确" : "证据核验错误";
                pendingResultText = isCorrect
                    ? currentTicket.OnSaveEvidenceText
                    : currentTicket.OnWrongEvidenceText;

                currentState = GameState.AwaitingDialogueCompletion;
                mainGameView.ShowEvidenceDialogue(
                    retainedEvidenceIndex,
                    isCorrect
                        ? currentTicket.CorrectEvidenceUserReply
                        : currentTicket.WrongEvidenceUserReply);
                return;
            }

            if (currentState != GameState.AwaitingDialogueCompletion)
            {
                return;
            }

            metricManager.Apply(
                pendingEvidenceCorrect
                    ? currentTicket.CorrectEvidenceMetricDelta
                    : currentTicket.WrongEvidenceMetricDelta);
            pendingEvidenceSaved =
                pendingEvidenceCorrect &&
                evidenceManager.SaveEvidence(currentTicket.EvidenceId);
            FinishTicket(
                pendingResultTitle,
                pendingResultText,
                pendingEvidenceSaved);
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

            metricManager.Apply(currentTicket.ResolvedMetricDelta);
            FinishTicket("工单已关闭", currentTicket.OnResolvedText, false);
        }

        /// <summary>
        /// 保存当前工单的处理状态并显示结果。
        /// </summary>
        private void FinishTicket(string resultTitle, string resultText, bool evidenceSaved)
        {
            processedTickets[currentTicketIndex] = true;
            processedCount++;
            currentState = GameState.ShowingResult;
            bool hasRemainingTickets = processedCount < currentStageTickets.Count;
            string resultActionLabel = hasRemainingTickets
                ? "返回工单列表"
                : HasLaterStage()
                    ? "进入下一阶段"
                    : "结束值班";
            mainGameView.RefreshQueueStates(currentTicketIndex, processedTickets);
            mainGameView.ShowResult(
                resultTitle,
                resultText,
                evidenceSaved,
                GetMetrics(),
                resultActionLabel);
        }

        /// <summary>
        /// 有剩余工单时返回左侧列表；阶段完成后进入下一阶段；
        /// 所有阶段完成后返回主菜单，后续可在该分支接入结局系统。
        /// </summary>
        private void HandleResultActionRequested()
        {
            if (currentState != GameState.ShowingResult)
            {
                return;
            }

            if (processedCount < currentStageTickets.Count)
            {
                currentTicket = null;
                currentTicketIndex = -1;
                ShowTicketSelection();
                return;
            }

            if (!LoadNextStage())
            {
                ReturnToTitle();
            }
        }

        private bool HasLaterStage()
        {
            for (int index = currentStageIndex + 1; index < stageOrder.Length; index++)
            {
                if (database.GetTicketsByStage(stageOrder[index]).Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetCurrentStageDisplayName()
        {
            if (currentStageIndex >= 0 &&
                currentStageIndex < stageDisplayNames.Length &&
                !string.IsNullOrWhiteSpace(stageDisplayNames[currentStageIndex]))
            {
                return stageDisplayNames[currentStageIndex];
            }

            return currentStageIndex >= 0 && currentStageIndex < stageOrder.Length
                ? stageOrder[currentStageIndex]
                : "当前阶段";
        }

        private GameMetrics GetMetrics()
        {
            return metricManager.GetSnapshot(evidenceManager.EvidenceCount);
        }

        /// <summary>
        /// 所有工单结束后返回主菜单。
        /// </summary>
        private void ReturnToTitle()
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

            if (metricManager == null)
            {
                Debug.LogError("GameFlowManager 缺少“指标管理器”引用。", this);
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

            if (stageOrder == null || stageOrder.Length == 0)
            {
                Debug.LogError("GameFlowManager 的“阶段顺序”至少需要配置一个阶段。", this);
                isValid = false;
            }

            return isValid;
        }
    }
}
