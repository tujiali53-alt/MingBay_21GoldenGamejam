using System;
using System.Collections.Generic;
using MingBay.Data;
using MingBay.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MingBay.Core
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MingBay/Level2/Core/Game Flow Manager")]
    public sealed class Level2GameFlowManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField]
        private MingBayProjectDatabase database;

        [SerializeField]
        private Level2KeywordEvidenceDatabase keywordEvidenceDatabase;

        [Header("Scene References")]
        [SerializeField]
        private EvidenceManager evidenceManager;

        [SerializeField]
        private MetricManager metricManager;

        [SerializeField]
        private Level1GameView mainGameView;

        [SerializeField]
        private Level2EvidenceChainController evidenceChainController;

        [Header("Stage")]
        [SerializeField]
        private string[] stageOrder = { "N2" };

        [SerializeField]
        private string[] stageDisplayNames = { "第二夜" };

        [Header("Scene")]
        [SerializeField]
        private string titleSceneName = "TitleScene";

        [SerializeField]
        private string nextLevelSceneName = "Level3Scene";

        [SerializeField]
        private string endingSceneName = "EndingScene";

        [SerializeField]
        private int autoClearEndingLimit = 3;

        private readonly List<TicketData> currentStageTickets = new();
        private Level2GameState currentState;
        private TicketData currentTicket;
        private Level2EvidenceChain currentEvidenceChain;
        private int currentStageIndex = -1;
        private int currentTicketIndex = -1;
        private int currentFollowUpIndex;
        private int selectedDataPanelIndex = -1;
        private int processedCount;
        private bool[] processedTickets;
        private bool isSceneLoading;
        private bool transferAppliedForCurrentTicket;

        private void Awake()
        {
            if (evidenceManager == null)
            {
                evidenceManager = GetComponent<EvidenceManager>();
            }

            if (evidenceManager == null)
            {
                evidenceManager = gameObject.AddComponent<EvidenceManager>();
            }

            if (metricManager == null)
            {
                metricManager = GetComponent<MetricManager>();
            }

            if (metricManager == null)
            {
                metricManager = gameObject.AddComponent<MetricManager>();
            }

            if (evidenceChainController == null)
            {
                evidenceChainController = GetComponent<Level2EvidenceChainController>();
            }

            if (evidenceChainController == null)
            {
                evidenceChainController = gameObject.AddComponent<Level2EvidenceChainController>();
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
            mainGameView.TicketAppClosed += HandleTicketAppClosed;
            mainGameView.FollowUpRequested += HandleFollowUpRequested;
            mainGameView.TransferHumanRequested += HandleTransferHumanRequested;
            mainGameView.SaveEvidenceRequested += HandleSaveEvidenceRequested;
            mainGameView.ChatEvidenceActionRequested += HandleChatEvidenceActionRequested;
            mainGameView.EvidenceSelected += HandleEvidenceSelected;
            mainGameView.EvidenceDetailKeywordClicked += HandleEvidenceDetailKeywordClicked;
            mainGameView.NotebookCancelRequested += HandleNotebookCancelRequested;
            mainGameView.MarkResolvedRequested += HandleMarkResolvedRequested;
            mainGameView.ResultActionRequested += HandleResultActionRequested;
            if (evidenceChainController != null)
            {
                mainGameView.SetEvidenceDetailFormatter(
                    evidenceChainController.FormatEvidenceDetailText);
            }
        }

        private void OnDisable()
        {
            if (mainGameView == null)
            {
                return;
            }

            mainGameView.TicketSelected -= HandleTicketSelected;
            mainGameView.ViewDataRequested -= HandleViewDataRequested;
            mainGameView.TicketAppClosed -= HandleTicketAppClosed;
            mainGameView.FollowUpRequested -= HandleFollowUpRequested;
            mainGameView.TransferHumanRequested -= HandleTransferHumanRequested;
            mainGameView.SaveEvidenceRequested -= HandleSaveEvidenceRequested;
            mainGameView.ChatEvidenceActionRequested -= HandleChatEvidenceActionRequested;
            mainGameView.EvidenceSelected -= HandleEvidenceSelected;
            mainGameView.EvidenceDetailKeywordClicked -= HandleEvidenceDetailKeywordClicked;
            mainGameView.NotebookCancelRequested -= HandleNotebookCancelRequested;
            mainGameView.MarkResolvedRequested -= HandleMarkResolvedRequested;
            mainGameView.ResultActionRequested -= HandleResultActionRequested;
            mainGameView.SetEvidenceDetailFormatter(null);
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

        private void StartGame()
        {
            evidenceManager.ResetEvidence();
            metricManager.ResetMetrics();
            currentStageIndex = -1;
            currentTicket = null;
            currentEvidenceChain = null;

            if (!LoadNextStage())
            {
                Debug.LogError("Level2 database has no playable N2 tickets.", database);
            }
        }

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
                currentEvidenceChain = null;
                processedTickets = new bool[currentStageTickets.Count];
                mainGameView.BuildTicketQueue(currentStageTickets);
                ShowTicketSelection();
                return true;
            }

            return false;
        }

        private void ShowTicketSelection()
        {
            currentState = Level2GameState.TicketSelection;
            mainGameView.ShowTicketSelection(
                GetCurrentStageDisplayName(),
                processedCount,
                currentStageTickets.Count,
                GetMetrics(),
                processedTickets);
        }

        private void HandleTicketSelected(int ticketIndex)
        {
            bool canSwitchTicket =
                currentState == Level2GameState.TicketSelection ||
                currentState == Level2GameState.ReadingTicket ||
                currentState == Level2GameState.ReviewingData;

            if (!canSwitchTicket ||
                ticketIndex < 0 ||
                ticketIndex >= currentStageTickets.Count ||
                processedTickets[ticketIndex])
            {
                return;
            }

            currentTicketIndex = ticketIndex;
            currentTicket = currentStageTickets[ticketIndex];
            currentEvidenceChain =
                keywordEvidenceDatabase.GetChainForTicket(currentTicket.TicketId);
            currentFollowUpIndex = 0;
            selectedDataPanelIndex = -1;
            transferAppliedForCurrentTicket = false;
            evidenceChainController.BeginTicket(currentTicket, currentEvidenceChain);

            currentState = Level2GameState.ReadingTicket;
            mainGameView.ShowTicket(
                currentTicket,
                GetCurrentStageDisplayName(),
                currentTicketIndex + 1,
                currentStageTickets.Count,
                GetMetrics());
            mainGameView.RefreshQueueStates(currentTicketIndex, processedTickets);
        }

        private void HandleViewDataRequested()
        {
            if (currentState != Level2GameState.ReadingTicket &&
                currentState != Level2GameState.ReviewingData)
            {
                return;
            }

            currentState = Level2GameState.ReviewingData;
            mainGameView.ShowData(currentTicket);
            mainGameView.SetEvidenceCollectionControlsVisible(false);
        }

        private void HandleFollowUpRequested()
        {
            if (currentState != Level2GameState.ReadingTicket &&
                currentState != Level2GameState.ReviewingData)
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
            mainGameView.ShowResidentFollowUp(
                currentTicket.UserName,
                followUpLine,
                currentFollowUpIndex < followUpLines.Length);
        }

        private void HandleTicketAppClosed()
        {
            selectedDataPanelIndex = -1;
            if (evidenceChainController != null)
            {
                evidenceChainController.HideTransientPanels();
            }

            if (currentState == Level2GameState.ReviewingData ||
                currentState == Level2GameState.AwaitingKeywordAnalysis)
            {
                currentState = currentTicket != null
                    ? Level2GameState.ReadingTicket
                    : Level2GameState.TicketSelection;
            }
        }

        private void HandleEvidenceSelected(int evidenceIndex)
        {
            if (currentState == Level2GameState.ReviewingData &&
                currentTicket != null &&
                currentTicket.HasEvidence)
            {
                selectedDataPanelIndex = evidenceIndex;
                mainGameView.ShowEvidenceDetailOnlySelected(evidenceIndex);
                return;
            }

            if (currentState == Level2GameState.AwaitingEvidencePresentation)
            {
                selectedDataPanelIndex = evidenceIndex;
            }
        }

        private void HandleSaveEvidenceRequested()
        {
            mainGameView.SetEvidenceCollectionControlsVisible(false);
        }

        private void HandleTransferHumanRequested()
        {
            if (currentState != Level2GameState.ReviewingData)
            {
                return;
            }

            if (!evidenceChainController.HasCollectedKeywords)
            {
                mainGameView.ShowRetainedEvidenceRequired();
                return;
            }

            if (!transferAppliedForCurrentTicket)
            {
                metricManager.Apply(currentTicket.TransferMetricDelta);
                transferAppliedForCurrentTicket = true;
            }

            currentState = Level2GameState.AwaitingKeywordAnalysis;
            TicketData ticket = currentTicket;
            mainGameView.PlayTicketDialogue(
                ticket.TransferDialogueLines,
                false,
                () =>
                {
                    if (currentTicket != ticket ||
                        currentState != Level2GameState.AwaitingKeywordAnalysis)
                    {
                        return;
                    }

                    evidenceChainController.ShowAnalysisPanel(HandleKeywordAnalysisClosed);
                });
        }

        private void HandleEvidenceDetailKeywordClicked(string slotId)
        {
            if (currentState != Level2GameState.ReviewingData)
            {
                return;
            }

            if (evidenceChainController.CollectKeywordBySlotId(slotId))
            {
                mainGameView.RefreshEvidenceDetailBody();
            }
        }

        private void HandleKeywordAnalysisClosed(bool hasAnalyzed)
        {
            if (currentState != Level2GameState.AwaitingKeywordAnalysis ||
                currentTicket == null)
            {
                return;
            }

            if (!hasAnalyzed)
            {
                currentState = Level2GameState.ReviewingData;
                mainGameView.ShowData(currentTicket);
                return;
            }

            currentState = Level2GameState.AwaitingEvidencePresentation;
            selectedDataPanelIndex = 0;
            mainGameView.ShowSingleEvidenceNotebook(
                currentTicket,
                evidenceChainController.CurrentAiSuggestion);
        }

        private void HandleChatEvidenceActionRequested()
        {
            if (currentState != Level2GameState.AwaitingEvidencePresentation)
            {
                return;
            }

            bool isCorrect = evidenceChainController.IsCurrentChainSatisfied();
            metricManager.Apply(
                isCorrect
                    ? currentTicket.CorrectEvidenceMetricDelta
                    : currentTicket.WrongEvidenceMetricDelta);
            bool evidenceSaved =
                isCorrect &&
                evidenceManager.SaveEvidence(currentTicket.EvidenceId);
            mainGameView.HideEvidenceNotebook();
            PlayEvidenceResultDialogueThenFinish(
                isCorrect,
                evidenceSaved,
                "提交成功",
                GetResultText(isCorrect));
        }

        private void HandleNotebookCancelRequested()
        {
            if (currentState != Level2GameState.AwaitingEvidencePresentation &&
                currentState != Level2GameState.AwaitingKeywordAnalysis)
            {
                return;
            }

            selectedDataPanelIndex = -1;
            currentState = Level2GameState.ReviewingData;
            mainGameView.HideEvidenceNotebook();
            mainGameView.ShowData(currentTicket);
        }

        private void HandleMarkResolvedRequested()
        {
            bool canForceResolveCurrentTicket =
                currentTicket != null &&
                processedTickets != null &&
                currentTicketIndex >= 0 &&
                currentTicketIndex < processedTickets.Length &&
                !processedTickets[currentTicketIndex] &&
                currentState != Level2GameState.TicketSelection &&
                currentState != Level2GameState.ShowingResult &&
                currentState != Level2GameState.AwaitingDialogueCompletion;
            if (!canForceResolveCurrentTicket)
            {
                return;
            }

            mainGameView.HideEvidenceNotebook();
            GameRunState.RecordAutoClear();
            metricManager.Apply(currentTicket.ResolvedMetricDelta);
            FinishTicket("工单已关闭", currentTicket.OnResolvedText, false);
        }

        private void PlayEvidenceResultDialogueThenFinish(
            bool isCorrect,
            bool evidenceSaved,
            string resultTitle,
            string resultText)
        {
            TicketData ticket = currentTicket;
            currentState = Level2GameState.AwaitingDialogueCompletion;
            TicketDialogueLine[] resultLines = isCorrect
                ? ticket.EvidenceCorrectDialogueLines
                : ticket.EvidenceWrongDialogueLines;
            mainGameView.PlayTicketDialogue(
                resultLines,
                false,
                () =>
                {
                    if (currentTicket != ticket ||
                        currentState != Level2GameState.AwaitingDialogueCompletion)
                    {
                        return;
                    }

                    FinishTicket(resultTitle, resultText, evidenceSaved);
                });
        }

        private void FinishTicket(
            string resultTitle,
            string resultText,
            bool evidenceSaved)
        {
            processedTickets[currentTicketIndex] = true;
            processedCount++;
            currentState = Level2GameState.ShowingResult;
            bool hasRemainingTickets = processedCount < currentStageTickets.Count;
            string resultActionLabel = hasRemainingTickets
                ? "返回工单列表"
                : HasLaterStage()
                    ? "进入下一阶段"
                    : ShouldLoadEndingScene()
                        ? "查看结局"
                        : ShouldLoadNextLevelScene()
                            ? GetNextLevelActionLabel()
                            : "结束值班";
            mainGameView.RefreshQueueStates(currentTicketIndex, processedTickets);
            mainGameView.ShowResult(
                resultTitle,
                resultText,
                evidenceSaved,
                GetMetrics(),
                resultActionLabel);
        }

        private void HandleResultActionRequested()
        {
            if (currentState != Level2GameState.ShowingResult)
            {
                return;
            }

            if (processedCount < currentStageTickets.Count)
            {
                currentTicket = null;
                currentEvidenceChain = null;
                currentTicketIndex = -1;
                evidenceChainController.BeginTicket(null, null);
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
            bool isEndingStage =
                string.Equals(finalStageId, "N3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(finalStageId, "FINAL", StringComparison.OrdinalIgnoreCase);
            return isEndingStage &&
                   GameRunState.GetEndingMetrics().AutoClearCount > autoClearEndingLimit;
        }

        private bool ShouldLoadNextLevelScene()
        {
            string sceneName = GetNextLevelSceneName();
            return !string.IsNullOrWhiteSpace(sceneName) &&
                   Application.CanStreamedLevelBeLoaded(sceneName);
        }

        private string GetNextLevelActionLabel()
        {
            string sceneName = GetNextLevelSceneName();
            return string.Equals(
                    sceneName,
                    "FinalConfrontationScene",
                    StringComparison.OrdinalIgnoreCase)
                ? "进入最终对峙"
                : "进入下一天";
        }

        private string GetResultText(bool isCorrect)
        {
            if (currentEvidenceChain != null)
            {
                string chainResult = isCorrect
                    ? currentEvidenceChain.CorrectResultText
                    : currentEvidenceChain.WrongResultText;
                if (!string.IsNullOrWhiteSpace(chainResult))
                {
                    return chainResult;
                }
            }

            return isCorrect
                ? currentTicket.OnSaveEvidenceText
                : currentTicket.OnWrongEvidenceText;
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

            return stageOrder ?? Array.Empty<string>();
        }

        private void ReturnToTitle()
        {
            if (isSceneLoading)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
            {
                Debug.LogError(
                    $"Unable to load scene '{titleSceneName}'. Check Build Settings.",
                    this);
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
                Debug.LogError(
                    $"Unable to load scene '{endingSceneName}'. Check Build Settings.",
                    this);
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
                Debug.LogError(
                    $"Unable to load next level scene '{sceneName}'. Check Build Settings.",
                    this);
                ReturnToTitle();
                return;
            }

            isSceneLoading = true;
            SceneManager.LoadSceneAsync(sceneName);
        }

        private string GetNextLevelSceneName()
        {
            return string.IsNullOrWhiteSpace(nextLevelSceneName)
                ? "Level3Scene"
                : nextLevelSceneName;
        }

        private bool ValidateReferences()
        {
            bool isValid = true;

            if (database == null)
            {
                Debug.LogError("Level2GameFlowManager is missing database.", this);
                isValid = false;
            }

            if (keywordEvidenceDatabase == null)
            {
                Debug.LogError("Level2GameFlowManager is missing keyword database.", this);
                isValid = false;
            }

            if (evidenceManager == null)
            {
                Debug.LogError("Level2GameFlowManager is missing EvidenceManager.", this);
                isValid = false;
            }

            if (metricManager == null)
            {
                Debug.LogError("Level2GameFlowManager is missing MetricManager.", this);
                isValid = false;
            }

            if (mainGameView == null)
            {
                Debug.LogError("Level2GameFlowManager is missing Level1GameView.", this);
                isValid = false;
            }

            if (evidenceChainController == null)
            {
                Debug.LogError("Level2GameFlowManager is missing keyword evidence controller.", this);
                isValid = false;
            }

            if (GetConfiguredStageOrder().Length == 0)
            {
                Debug.LogError("Level2GameFlowManager needs at least one stage id.", this);
                isValid = false;
            }

            return isValid;
        }
    }
}
