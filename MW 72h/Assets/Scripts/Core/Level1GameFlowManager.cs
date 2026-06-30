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
    [AddComponentMenu("明湾/Level1/核心/游戏流程管理器")]
    public sealed class Level1GameFlowManager : MonoBehaviour
    {
        [Header("数据配置")]
        [SerializeField]
        [InspectorName("Demo 数据库")]
        [Tooltip("包含本次 Demo 工单顺序的数据资产。策划调整工单时修改该资产即可。")]
        private MingBayProjectDatabase database;

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
        [Tooltip("负责显示工单、资料、按钮和结果的 Level1GameView。")]
        private Level1GameView mainGameView;

        [Header("阶段配置")]
        [SerializeField]
        [InspectorName("阶段顺序")]
        [Tooltip("按游戏实际推进顺序填写阶段 ID。默认先教程，再进入第一天正式值班。")]
        private string[] stageOrder = { "N1" };

        [SerializeField]
        [InspectorName("阶段显示名称")]
        [Tooltip("与阶段顺序一一对应，用于顶部状态和阶段完成按钮。")]
        private string[] stageDisplayNames = { "第一夜" };

        [Header("场景配置")]
        [SerializeField]
        [InspectorName("主菜单场景名称")]
        [Tooltip("点击结果面板中的“返回主菜单”后加载的场景。必须存在于 Build Settings。")]
        private string titleSceneName = "TitleScene";

        [SerializeField]
        [InspectorName("下一关场景名称")]
        [Tooltip("当前关卡完成且不是最终阶段时加载的场景。第一天默认进入 Level2Scene。")]
        private string nextLevelSceneName = "Level2Scene";

        [SerializeField]
        [InspectorName("结局场景名称")]
        [Tooltip("最终阶段完成后加载的结局场景。当前阶段顺序以 N3 或 FINAL 结束时生效。")]
        private string endingSceneName = "EndingScene";

        private Level1GameState currentState;
        private readonly List<TicketData> currentStageTickets = new();
        private TicketData currentTicket;
        private int currentStageIndex = -1;
        private int currentTicketIndex;
        private int processedCount;
        private bool[] processedTickets;
        private bool isSceneLoading;
        private int selectedEvidenceIndex = -1;
        private int retainedEvidenceIndex = -1;
        private readonly HashSet<int> retainedEvidenceIndices = new();
        private int currentFollowUpIndex;
        private bool transferAppliedForCurrentTicket;
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
            mainGameView.NotebookCancelRequested += HandleNotebookCancelRequested;
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
            mainGameView.NotebookCancelRequested -= HandleNotebookCancelRequested;
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
            string[] configuredStageOrder = GetConfiguredStageOrder();
            while (++currentStageIndex < configuredStageOrder.Length)
            {
                currentStageTickets.Clear();
                currentStageTickets.AddRange(
                    database.GetTicketsByStage(configuredStageOrder[currentStageIndex]));

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
            currentState = Level1GameState.TicketSelection;
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
                currentState == Level1GameState.TicketSelection ||
                currentState == Level1GameState.ReadingTicket ||
                currentState == Level1GameState.ReviewingData;

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
            retainedEvidenceIndices.Clear();
            currentFollowUpIndex = 0;
            transferAppliedForCurrentTicket = false;
            currentState = Level1GameState.ReadingTicket;
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
            if (currentState != Level1GameState.ReadingTicket &&
                currentState != Level1GameState.ReviewingData)
            {
                return;
            }

            currentState = Level1GameState.ReviewingData;
            mainGameView.ShowData(currentTicket);
        }

        /// <summary>
        /// 追问用户；需要核验证据的工单进入证据选择阶段。
        /// </summary>
        private void HandleFollowUpRequested()
        {
            if (currentState != Level1GameState.ReadingTicket &&
                currentState != Level1GameState.ReviewingData)
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
            mainGameView.ShowResidentFollowUp(
                currentTicket.UserName,
                followUpLine,
                hasRemainingFollowUp);

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
            if (currentState != Level1GameState.ReviewingData)
            {
                return;
            }

            if (currentTicket.RequiresEvidenceSelection &&
                retainedEvidenceIndices.Count == 0)
            {
                mainGameView.ShowRetainedEvidenceRequired();
                return;
            }

            bool shouldPlayTransferDialogue = !transferAppliedForCurrentTicket;
            if (!transferAppliedForCurrentTicket)
            {
                metricManager.Apply(currentTicket.TransferMetricDelta);
                transferAppliedForCurrentTicket = true;
            }

            if (currentTicket.RequiresEvidenceSelection &&
                retainedEvidenceIndices.Count > 0)
            {
                currentState = Level1GameState.AwaitingEvidencePresentation;
                retainedEvidenceIndex = -1;
                TicketData ticket = currentTicket;
                List<int> retainedEvidenceSnapshot = new(retainedEvidenceIndices);
                if (shouldPlayTransferDialogue)
                {
                    mainGameView.PlayTicketDialogue(
                        ticket.TransferDialogueLines,
                        false,
                        () =>
                        {
                            if (currentState == Level1GameState.AwaitingEvidencePresentation &&
                                currentTicket == ticket)
                            {
                                mainGameView.ShowEvidenceNotebook(
                                    ticket,
                                    retainedEvidenceSnapshot);
                            }
                        });
                }
                else
                {
                    mainGameView.ShowEvidenceNotebook(ticket, retainedEvidenceSnapshot);
                }

                return;
            }

            if (shouldPlayTransferDialogue)
            {
                TicketData ticket = currentTicket;
                mainGameView.PlayTicketDialogue(
                    ticket.TransferDialogueLines,
                    false,
                    () =>
                    {
                        if (currentTicket == ticket)
                        {
                            BeginEvidenceOrResolutionHint(string.Empty);
                        }
                    });
            }
            else
            {
                BeginEvidenceOrResolutionHint(string.Empty);
            }
        }

        /// <summary>
        /// 教程中直接保留当前工单的有效证据，但不结束工单。
        /// </summary>
        private void HandleSaveEvidenceRequested()
        {
            if (currentState == Level1GameState.AwaitingEvidence &&
                selectedEvidenceIndex >= 0)
            {
                ResolveEvidence(selectedEvidenceIndex);
                return;
            }

            if (currentState != Level1GameState.ReviewingData ||
                !currentTicket.HasEvidence ||
                selectedEvidenceIndex < 0)
            {
                return;
            }

            retainedEvidenceIndex = selectedEvidenceIndex;
            retainedEvidenceIndices.Add(selectedEvidenceIndex);
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

            currentState = Level1GameState.AwaitingEvidence;
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
            if (currentState == Level1GameState.ReviewingData &&
                currentTicket.HasEvidence)
            {
                selectedEvidenceIndex = evidenceIndex;
                mainGameView.ShowEvidenceCandidateSelected(evidenceIndex);
                return;
            }

            if (currentState == Level1GameState.AwaitingEvidence)
            {
                selectedEvidenceIndex = evidenceIndex;
                mainGameView.ShowEvidenceCandidateSelected(evidenceIndex);
                return;
            }

            if (currentState == Level1GameState.AwaitingEvidencePresentation &&
                retainedEvidenceIndices.Contains(evidenceIndex))
            {
                retainedEvidenceIndex = evidenceIndex;
                return;
            }
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
                    currentState = Level1GameState.ReviewingData;
                    mainGameView.ShowTutorialEvidenceAccepted(
                        currentTicket.OnSaveEvidenceText,
                        GetMetrics());
                    return;
                }

                PlayEvidenceResultDialogueThenFinish(
                    true,
                    evidenceSaved,
                    "\u63d0\u4ea4\u6210\u529f",
                    currentTicket.OnSaveEvidenceText);
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

            PlayEvidenceResultDialogueThenFinish(
                false,
                false,
                "\u63d0\u4ea4\u6210\u529f",
                currentTicket.OnWrongEvidenceText);
        }

        /// <summary>
        /// 处理聊天区域中的“出示证据”和“完成对话”按钮。
        /// </summary>
        private void HandleChatEvidenceActionRequested()
        {
            if (currentState == Level1GameState.AwaitingEvidencePresentation)
            {
                if (retainedEvidenceIndex < 0)
                {
                    return;
                }

                bool isCorrect =
                    currentTicket.HasEvidence &&
                    retainedEvidenceIndex == currentTicket.CorrectEvidenceIndex;

                metricManager.Apply(
                    isCorrect
                        ? currentTicket.CorrectEvidenceMetricDelta
                        : currentTicket.WrongEvidenceMetricDelta);
                bool evidenceSaved =
                    isCorrect &&
                    evidenceManager.SaveEvidence(currentTicket.EvidenceId);
                string feedbackText = isCorrect
                    ? currentTicket.OnSaveEvidenceText
                    : currentTicket.OnWrongEvidenceText;
                mainGameView.HideEvidenceNotebook();
                PlayEvidenceResultDialogueThenFinish(
                    isCorrect,
                    evidenceSaved,
                    "\u63d0\u4ea4\u6210\u529f",
                    feedbackText);
                return;
            }

            if (currentState != Level1GameState.AwaitingDialogueCompletion)
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

        private void HandleNotebookCancelRequested()
        {
            if (currentState != Level1GameState.AwaitingEvidencePresentation)
            {
                return;
            }

            retainedEvidenceIndex = -1;
            currentState = Level1GameState.ReviewingData;
            mainGameView.ShowData(currentTicket);
        }

        /// <summary>
        /// 将工单标记为已解决，并记录已解决数量。
        /// </summary>
        private void HandleMarkResolvedRequested()
        {
            bool canForceResolveCurrentTicket =
                currentTicket != null &&
                processedTickets != null &&
                currentTicketIndex >= 0 &&
                currentTicketIndex < processedTickets.Length &&
                !processedTickets[currentTicketIndex] &&
                currentState != Level1GameState.TicketSelection &&
                currentState != Level1GameState.ShowingResult &&
                currentState != Level1GameState.AwaitingDialogueCompletion;
            if (!canForceResolveCurrentTicket)
            {
                return;
            }

            mainGameView.HideEvidenceNotebook();
            GameRunState.RecordAutoClear();
            metricManager.Apply(currentTicket.ResolvedMetricDelta);
            FinishTicket("工单已关闭", currentTicket.OnResolvedText, false);
        }

        /// <summary>
        /// 保存当前工单的处理状态并显示结果。
        /// </summary>
        private void PlayEvidenceResultDialogueThenFinish(
            bool isCorrect,
            bool evidenceSaved,
            string resultTitle,
            string resultText)
        {
            TicketData ticket = currentTicket;
            currentState = Level1GameState.AwaitingDialogueCompletion;
            TicketDialogueLine[] resultLines = isCorrect
                ? ticket.EvidenceCorrectDialogueLines
                : ticket.EvidenceWrongDialogueLines;
            mainGameView.PlayTicketDialogue(
                resultLines,
                false,
                () =>
                {
                    if (currentTicket != ticket ||
                        currentState != Level1GameState.AwaitingDialogueCompletion)
                    {
                        return;
                    }

                    FinishTicket(resultTitle, resultText, evidenceSaved);
                });
        }


        private void FinishTicket(string resultTitle, string resultText, bool evidenceSaved)
        {
            processedTickets[currentTicketIndex] = true;
            processedCount++;
            currentState = Level1GameState.ShowingResult;
            bool hasRemainingTickets = processedCount < currentStageTickets.Count;
            string resultActionLabel = hasRemainingTickets
                ? "返回工单列表"
                : HasLaterStage()
                    ? "进入下一阶段"
                    : ShouldLoadEndingScene()
                        ? "查看结局"
                        : ShouldLoadNextLevelScene()
                            ? "进入下一天"
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
            if (currentState != Level1GameState.ShowingResult)
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
                if (ShouldLoadEndingScene())
                {
                    LoadEndingScene();
                }
                else if (ShouldLoadNextLevelScene())
                {
                    LoadNextLevelScene();
                }
                else
                {
                    ReturnToTitle();
                }
            }
        }

        private bool HasLaterStage()
        {
            string[] configuredStageOrder = GetConfiguredStageOrder();
            for (int index = currentStageIndex + 1; index < configuredStageOrder.Length; index++)
            {
                if (database.GetTicketsByStage(configuredStageOrder[index]).Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldLoadEndingScene()
        {
            string[] configuredStageOrder = GetConfiguredStageOrder();
            if (configuredStageOrder.Length == 0)
            {
                return false;
            }

            string finalStageId = configuredStageOrder[configuredStageOrder.Length - 1];
            return string.Equals(finalStageId, "N3", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(finalStageId, "FINAL", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldLoadNextLevelScene()
        {
            string sceneName = GetNextLevelSceneName();
            return !string.IsNullOrWhiteSpace(sceneName) &&
                   Application.CanStreamedLevelBeLoaded(sceneName);
        }

        private string GetCurrentStageDisplayName()
        {
            if (database != null)
            {
                string databaseStageName = database.GetStageDisplayName(currentStageIndex);
                if (!string.IsNullOrWhiteSpace(databaseStageName))
                {
                    return databaseStageName;
                }
            }

            if (currentStageIndex >= 0 &&
                currentStageIndex < stageDisplayNames.Length &&
                !string.IsNullOrWhiteSpace(stageDisplayNames[currentStageIndex]))
            {
                return stageDisplayNames[currentStageIndex];
            }

            string[] configuredStageOrder = GetConfiguredStageOrder();
            return currentStageIndex >= 0 && currentStageIndex < configuredStageOrder.Length
                ? configuredStageOrder[currentStageIndex]
                : "当前阶段";
        }

        private GameMetrics GetMetrics()
        {
            return metricManager.GetSnapshot(evidenceManager.EvidenceCount);
        }

        private string[] GetConfiguredStageOrder()
        {
            if (database != null && database.StageOrderArray.Length > 0)
            {
                return database.StageOrderArray;
            }

            return stageOrder ?? System.Array.Empty<string>();
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

        private void LoadEndingScene()
        {
            if (isSceneLoading)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(endingSceneName))
            {
                Debug.LogError($"无法加载场景“{endingSceneName}”，请检查 Build Settings。", this);
                ReturnToTitle();
                return;
            }

            isSceneLoading = true;
            SceneManager.LoadSceneAsync(endingSceneName);
        }

        private void LoadNextLevelScene()
        {
            if (isSceneLoading)
            {
                return;
            }

            string sceneName = GetNextLevelSceneName();
            if (string.IsNullOrWhiteSpace(sceneName) ||
                !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"无法加载下一关场景“{sceneName}”，请检查 Build Settings。", this);
                ReturnToTitle();
                return;
            }

            isSceneLoading = true;
            SceneManager.LoadSceneAsync(sceneName);
        }

        private string GetNextLevelSceneName()
        {
            return string.IsNullOrWhiteSpace(nextLevelSceneName)
                ? "Level2Scene"
                : nextLevelSceneName;
        }

        /// <summary>
        /// 检查场景中必须绑定的引用，并输出策划可理解的中文错误。
        /// </summary>
        private bool ValidateReferences()
        {
            bool isValid = true;

            if (database == null)
            {
                Debug.LogError("Level1GameFlowManager 缺少“Demo 数据库”引用。", this);
                isValid = false;
            }

            if (evidenceManager == null)
            {
                Debug.LogError("Level1GameFlowManager 缺少“证据管理器”引用。", this);
                isValid = false;
            }

            if (metricManager == null)
            {
                Debug.LogError("Level1GameFlowManager 缺少“指标管理器”引用。", this);
                isValid = false;
            }

            if (mainGameView == null)
            {
                Debug.LogError("Level1GameFlowManager 缺少“游戏主界面”引用。", this);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(titleSceneName))
            {
                Debug.LogError("Level1GameFlowManager 的“主菜单场景名称”不能为空。", this);
                isValid = false;
            }

            if (GetConfiguredStageOrder().Length == 0)
            {
                Debug.LogError("Level1GameFlowManager 的“阶段顺序”至少需要配置一个阶段。", this);
                isValid = false;
            }

            return isValid;
        }
    }
}
