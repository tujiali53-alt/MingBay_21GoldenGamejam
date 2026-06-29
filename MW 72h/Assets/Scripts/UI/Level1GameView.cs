using System;
using System.Collections;
using System.Collections.Generic;
using MingBay.Core;
using MingBay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MingBay.UI
{
    /// <summary>
    /// 游戏主界面的显示与按钮入口。
    /// 该脚本不判断证据和流程，只负责把玩家操作通知给 Level1GameFlowManager。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("明湾/Level1/UI/游戏主界面")]
    public sealed class Level1GameView : MonoBehaviour
    {
        [Header("桌面入口")]
        [SerializeField]
        [InspectorName("工单 APP 窗口")]
        [Tooltip("点击桌面或任务栏入口后显示的工单窗口根节点。")]
        private GameObject ticketAppWindow;

        [SerializeField]
        [InspectorName("Ticket App Close Button")]
        private Button ticketAppCloseButton;

        [SerializeField]
        [InspectorName("桌面工单 APP 按钮")]
        [Tooltip("主界面左上角的工单 APP 入口。")]
        private Button workAppButton;

        [SerializeField]
        [InspectorName("桌面线索笔记按钮")]
        [Tooltip("主界面左上角的线索笔记入口。")]
        private Button clueNotebookButton;

        [SerializeField]
        [InspectorName("任务栏工单队列按钮")]
        [Tooltip("任务栏中的工单队列入口。")]
        private Button taskbarWorkQueueButton;

        [SerializeField]
        [InspectorName("任务栏资料库按钮")]
        [Tooltip("任务栏中的资料库入口。")]
        private Button taskbarDatabaseButton;

        [Header("工单队列")]
        [SerializeField]
        [InspectorName("工单队列容器")]
        [Tooltip("运行时生成工单按钮的父节点。左侧工单会按数据库顺序排列。")]
        private RectTransform ticketQueueContent;

        [SerializeField]
        [InspectorName("工单队列模板")]
        [Tooltip("用于复制工单按钮的隐藏模板，必须绑定 Level1TicketQueueItemView。")]
        private Level1TicketQueueItemView ticketQueueItemTemplate;

        [SerializeField]
        [InspectorName("工单内容对象")]
        [Tooltip("未选择工单时需要隐藏的对话、资料和操作区域。")]
        private GameObject[] ticketContentObjects;

        [Header("顶部状态")]
        [SerializeField]
        [InspectorName("工单编号文本")]
        [Tooltip("显示当前工单编号和总数，例如“工单 1 / 4”。")]
        private TMP_Text ticketProgressText;

        [SerializeField]
        [InspectorName("流程状态文本")]
        [Tooltip("显示当前步骤，例如“等待核验”“资料已展开”或“处理完成”。")]
        private TMP_Text statusText;

        [SerializeField]
        [InspectorName("证据数量文本")]
        [Tooltip("显示本局已经保存的证据数量。")]
        private TMP_Text evidenceCountText;

        [SerializeField]
        [InspectorName("已解决数量文本")]
        [Tooltip("显示本局已经标记为已解决的工单数量。")]
        private TMP_Text resolvedCountText;

        [Header("工单信息")]
        [SerializeField]
        [InspectorName("工单标题文本")]
        [Tooltip("显示当前工单标题。")]
        private TMP_Text ticketTitleText;

        [SerializeField]
        [InspectorName("工单摘要文本")]
        [Tooltip("显示用户、用户情绪和区域。")]
        private TMP_Text ticketMetaText;

        [SerializeField]
        [InspectorName("工单号文本")]
        [Tooltip("显示当前工单编号，固定在工单信息面板右下角。")]
        private TMP_Text ticketIdText;

        [SerializeField]
        [InspectorName("用户消息文本")]
        [Tooltip("显示居民提交的求助内容。")]
        private TMP_Text userMessageText;

        [SerializeField]
        [InspectorName("AI 回复文本")]
        [Tooltip("显示系统自动回复内容。")]
        private TMP_Text aiReplyText;

        [SerializeField]
        [InspectorName("聊天滚动组件")]
        [Tooltip("聊天区 ScrollRect。新内容出现后会自动滚动到底部。")]
        private ScrollRect chatScrollRect;

        [SerializeField]
        [InspectorName("聊天内容容器")]
        [Tooltip("运行时生成对话气泡的父节点。")]
        private RectTransform chatContent;

        [SerializeField]
        [InspectorName("聊天气泡模板")]
        [Tooltip("隐藏模板，运行时复制生成用户和系统对话。")]
        private GameObject chatBubbleTemplate;

        [SerializeField]
        [InspectorName("聊天空状态")]
        [Tooltip("未选择工单时显示在聊天区中央的提示。")]
        private GameObject chatEmptyState;

        [SerializeField]
        [InspectorName("对话间隔秒数")]
        [Tooltip("连续生成对话时，每条之间停留的时间。")]
        private float dialogueRevealDelay = 0.75f;

        [SerializeField]
        [Min(0f)]
        [InspectorName("Popup Delay After Dialogue")]
        [Tooltip("Delay before opening notebook/result popups after the final dialogue bubble appears.")]
        private float popupDelayAfterDialogueSeconds = 0.35f;

        [Header("资料面板")]
        [SerializeField]
        [InspectorName("资料面板对象")]
        [Tooltip("点击“查看资料”后显示的完整资料面板。")]
        private GameObject dataPanel;

        [SerializeField]
        [InspectorName("资料滚动组件")]
        [Tooltip("右侧资料库窗口的 ScrollRect，资料过多时可滚轮滚动。")]
        private ScrollRect dataScrollRect;

        [SerializeField]
        [InspectorName("用户资料文本")]
        [Tooltip("显示居民基本资料。")]
        private TMP_Text profileText;

        [SerializeField]
        [InspectorName("历史工单文本")]
        [Tooltip("显示用户或同区域的历史工单记录。")]
        private TMP_Text historyText;

        [SerializeField]
        [InspectorName("设备日志文本")]
        [Tooltip("显示设备上报日志。")]
        private TMP_Text deviceLogText;

        [SerializeField]
        [InspectorName("区域状态文本")]
        [Tooltip("显示区域标签或维护状态。")]
        private TMP_Text regionStatusText;

        [SerializeField]
        private GameObject evidenceDetailOverlay;

        [SerializeField]
        private TMP_Text evidenceDetailTitleText;

        [SerializeField]
        private TMP_Text evidenceDetailBodyText;

        [SerializeField]
        private Button evidenceDetailCloseButton;

        [Header("操作按钮")]
        [SerializeField]
        [FormerlySerializedAs("viewDataButton")]
        [InspectorName("主操作按钮")]
        [Tooltip("追问按钮。有下一句追问时为绿色，没有后续追问时变灰。")]
        private Button primaryActionButton;

        [SerializeField]
        [InspectorName("资料库查找按钮")]
        [Tooltip("点击后展开或刷新资料区。")]
        private Button dataLookupButton;

        [SerializeField]
        [FormerlySerializedAs("saveEvidenceButton")]
        [InspectorName("转人工按钮")]
        [Tooltip("将当前工单转交人工部门。查看资料前保持不可点击。")]
        private Button transferHumanButton;

        [SerializeField]
        [InspectorName("保留证据按钮")]
        [Tooltip("确认资料面板中当前选中的证据。转人工后会自动出示该证据，并根据正确与否进入对应回答分支。")]
        private Button saveEvidenceButton;

        [SerializeField]
        [InspectorName("标记已解决按钮")]
        [Tooltip("玩家长按后关闭工单。查看资料前保持不可点击。")]
        private Button markResolvedButton;

        [SerializeField]
        [InspectorName("标记已解决长按读条")]
        [Tooltip("绑定到按钮内部的 Image，长按时从 0 填充到 1。")]
        private Image markResolvedHoldFill;

        [SerializeField]
        [InspectorName("标记已解决长按秒数")]
        [Tooltip("暂定 1 秒。长按达到该时长后才触发关闭工单。")]
        private float markResolvedHoldSeconds = 1f;

        [SerializeField]
        [InspectorName("聊天证据操作按钮")]
        [Tooltip("显示在 AI 聊天气泡中的“出示证据/完成对话”按钮。旧场景未绑定时会在运行时自动创建。")]
        private Button chatEvidenceActionButton;

        [Header("笔记面板")]
        [SerializeField]
        [InspectorName("笔记面板对象")]
        [Tooltip("转人工后显示的笔记面板，用于选择已收集资料并提交证据。")]
        private GameObject notebookPanel;

        [SerializeField]
        [InspectorName("笔记工单原因")]
        private TMP_Text notebookReasonText;

        [SerializeField]
        [InspectorName("笔记用户名")]
        private TMP_Text notebookUserText;

        [SerializeField]
        [InspectorName("笔记用户情绪")]
        private TMP_Text notebookEmotionText;

        [SerializeField]
        [InspectorName("笔记区域信息")]
        private TMP_Text notebookRegionText;

        [SerializeField]
        [InspectorName("笔记工单号")]
        private TMP_Text notebookTicketIdText;

        [SerializeField]
        [InspectorName("笔记资料按钮")]
        private Button[] notebookEvidenceButtons;

        [SerializeField]
        [InspectorName("笔记资料文本")]
        private TMP_Text[] notebookEvidenceTexts;

        [SerializeField]
        [InspectorName("笔记资料高亮")]
        private Outline[] notebookEvidenceOutlines;

        [SerializeField]
        [InspectorName("笔记关闭按钮")]
        private Button notebookCloseButton;

        [SerializeField]
        [InspectorName("我再想想按钮")]
        private Button notebookCancelButton;

        [SerializeField]
        [InspectorName("提交证据按钮")]
        private Button notebookSubmitButton;

        [Header("结果面板")]
        [SerializeField]
        [InspectorName("结果面板对象")]
        [Tooltip("完成当前工单后显示的结果覆盖面板。")]
        private GameObject resultPanel;

        [SerializeField]
        [InspectorName("结果标题文本")]
        [Tooltip("显示“证据核验正确”“证据核验错误”或“工单已关闭”等结果标题。")]
        private TMP_Text resultTitleText;

        [SerializeField]
        [InspectorName("结果状态文本")]
        [Tooltip("显示工单已关闭等结算状态。")]
        private TMP_Text resultStatusText;

        [SerializeField]
        [InspectorName("结果说明文本")]
        [Tooltip("显示该选择造成的具体反馈。")]
        private TMP_Text resultDescriptionText;

        [SerializeField]
        [InspectorName("结果统计文本")]
        [Tooltip("显示当前证据数与已解决数。")]
        private TMP_Text resultMetricsText;

        [SerializeField]
        [FormerlySerializedAs("returnToTitleButton")]
        [InspectorName("结果操作按钮")]
        [Tooltip("还有未处理工单时返回工单列表；阶段完成后进入下一阶段；所有阶段完成后结束值班。")]
        private Button resultActionButton;

        public event Action<int> TicketSelected;
        public event Action ViewDataRequested;
        public event Action FollowUpRequested;
        public event Action TransferHumanRequested;
        public event Action SaveEvidenceRequested;
        public event Action ChatEvidenceActionRequested;
        public event Action<int> EvidenceSelected;
        public event Action<int> EvidenceDetailOpened;
        public event Action<string> EvidenceDetailKeywordClicked;
        public event Action NotebookCancelRequested;
        public event Action MarkResolvedRequested;
        public event Action ResultActionRequested;

        private const int NotebookEvidenceSlotCount = 4;

        private readonly List<Level1TicketQueueItemView> queueItems = new();
        private readonly Button[] evidenceButtons = new Button[4];
        private readonly Button[] evidenceDetailButtons = new Button[4];
        private readonly Button[] evidenceCollectButtons = new Button[4];
        private readonly GameObject[] evidenceActionRows = new GameObject[4];
        private readonly LayoutElement[] evidenceCardLayouts = new LayoutElement[4];
        private TMP_Text resultActionButtonText;
        private TMP_Text primaryActionButtonText;
        private TMP_Text transferHumanButtonText;
        private TMP_Text saveEvidenceButtonText;
        private TMP_Text dataLookupButtonText;
        private TMP_Text chatEvidenceActionButtonText;
        private bool resolveTutorialHintVisible;
        private ColorBlock defaultMarkResolvedColors;
        private ColorBlock defaultFollowUpColors;
        private Coroutine dialogueRoutine;
        private bool isHoldingMarkResolved;
        private float markResolvedHoldTimer;
        private bool markResolvedHoldTriggered;
        private bool hasActiveTicketContent;
        private int selectedNotebookEvidenceIndex = -1;
        private Func<int, string, string> evidenceDetailFormatter;
        private readonly List<EvidenceDetailLinkHandler> evidenceDetailLinkHandlers = new();
        private int currentEvidenceDetailIndex = -1;

        private void Awake()
        {
            EnsureRuntimeReferences();
            CacheButtonLabels();
            EnsureEvidenceButtons();
            ConfigureConversationText();
            if (markResolvedButton != null)
            {
                ApplyMarkResolvedDefaultColors();
                ConfigureLongPressButton(markResolvedButton);
            }

            if (primaryActionButton != null)
            {
                defaultFollowUpColors = primaryActionButton.colors;
            }

            SetTicketAppWindowVisible(false);
            SetTicketContentVisible(true);
            SetDataPanelVisible(false);
            SetNotebookPanelVisible(false);
            SetEvidenceDetailVisible(false);
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (workAppButton != null)
            {
                workAppButton.onClick.AddListener(OpenWorkApp);
            }

            if (ticketAppCloseButton != null)
            {
                ticketAppCloseButton.onClick.AddListener(CloseWorkApp);
            }

            if (taskbarWorkQueueButton != null)
            {
                taskbarWorkQueueButton.onClick.AddListener(OpenWorkApp);
            }

            if (clueNotebookButton != null)
            {
                clueNotebookButton.onClick.AddListener(OpenClueNotebook);
            }

            if (taskbarDatabaseButton != null)
            {
                taskbarDatabaseButton.onClick.AddListener(OpenWorkApp);
                taskbarDatabaseButton.onClick.AddListener(NotifyViewDataRequested);
            }

            if (primaryActionButton != null)
            {
                primaryActionButton.onClick.AddListener(NotifyFollowUpRequested);
            }

            if (dataLookupButton != null)
            {
                dataLookupButton.onClick.AddListener(NotifyViewDataRequested);
            }

            if (transferHumanButton != null)
            {
                transferHumanButton.onClick.AddListener(NotifyTransferHumanRequested);
            }

            if (saveEvidenceButton != null)
            {
                saveEvidenceButton.onClick.AddListener(NotifySaveEvidenceRequested);
            }

            if (chatEvidenceActionButton != null)
            {
                chatEvidenceActionButton.onClick.AddListener(NotifyChatEvidenceActionRequested);
            }

            if (notebookCloseButton != null)
            {
                notebookCloseButton.onClick.AddListener(NotifyNotebookCancelRequested);
            }

            if (notebookCancelButton != null)
            {
                notebookCancelButton.onClick.AddListener(NotifyNotebookCancelRequested);
            }

            if (notebookSubmitButton != null)
            {
                notebookSubmitButton.onClick.AddListener(NotifyChatEvidenceActionRequested);
            }

            AddNotebookEvidenceListeners();

            if (resultActionButton != null)
            {
                resultActionButton.onClick.AddListener(NotifyResultActionRequested);
            }

            if (evidenceDetailCloseButton != null)
            {
                evidenceDetailCloseButton.onClick.AddListener(CloseEvidenceDetail);
            }
        }

        private void OnDisable()
        {
            if (workAppButton != null)
            {
                workAppButton.onClick.RemoveListener(OpenWorkApp);
            }

            if (ticketAppCloseButton != null)
            {
                ticketAppCloseButton.onClick.RemoveListener(CloseWorkApp);
            }

            if (taskbarWorkQueueButton != null)
            {
                taskbarWorkQueueButton.onClick.RemoveListener(OpenWorkApp);
            }

            if (clueNotebookButton != null)
            {
                clueNotebookButton.onClick.RemoveListener(OpenClueNotebook);
            }

            if (taskbarDatabaseButton != null)
            {
                taskbarDatabaseButton.onClick.RemoveListener(OpenWorkApp);
                taskbarDatabaseButton.onClick.RemoveListener(NotifyViewDataRequested);
            }

            if (primaryActionButton != null)
            {
                primaryActionButton.onClick.RemoveListener(NotifyFollowUpRequested);
            }

            if (dataLookupButton != null)
            {
                dataLookupButton.onClick.RemoveListener(NotifyViewDataRequested);
            }

            if (transferHumanButton != null)
            {
                transferHumanButton.onClick.RemoveListener(NotifyTransferHumanRequested);
            }

            if (saveEvidenceButton != null)
            {
                saveEvidenceButton.onClick.RemoveListener(NotifySaveEvidenceRequested);
            }

            if (chatEvidenceActionButton != null)
            {
                chatEvidenceActionButton.onClick.RemoveListener(NotifyChatEvidenceActionRequested);
            }

            if (notebookCloseButton != null)
            {
                notebookCloseButton.onClick.RemoveListener(NotifyNotebookCancelRequested);
            }

            if (notebookCancelButton != null)
            {
                notebookCancelButton.onClick.RemoveListener(NotifyNotebookCancelRequested);
            }

            if (notebookSubmitButton != null)
            {
                notebookSubmitButton.onClick.RemoveListener(NotifyChatEvidenceActionRequested);
            }

            RemoveNotebookEvidenceListeners();

            if (resultActionButton != null)
            {
                resultActionButton.onClick.RemoveListener(NotifyResultActionRequested);
            }

            if (evidenceDetailCloseButton != null)
            {
                evidenceDetailCloseButton.onClick.RemoveListener(CloseEvidenceDetail);
            }
        }

        private void Update()
        {
            if (!isHoldingMarkResolved || markResolvedHoldTriggered)
            {
                return;
            }

            markResolvedHoldTimer += Time.unscaledDeltaTime;
            float progress = markResolvedHoldSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(markResolvedHoldTimer / markResolvedHoldSeconds);
            SetResolveHoldFill(progress);

            if (progress < 1f)
            {
                return;
            }

            markResolvedHoldTriggered = true;
            isHoldingMarkResolved = false;
            NotifyMarkResolvedRequested();
        }

        /// <summary>
        /// 根据数据库生成左侧工单按钮。界面不读取配置文件，只消费传入的数据。
        /// </summary>
        public void BuildTicketQueue(IReadOnlyList<TicketData> tickets)
        {
            for (int childIndex = ticketQueueContent.childCount - 1; childIndex >= 0; childIndex--)
            {
                Level1TicketQueueItemView existingItem =
                    ticketQueueContent.GetChild(childIndex).GetComponent<Level1TicketQueueItemView>();
                if (existingItem == null || existingItem == ticketQueueItemTemplate)
                {
                    continue;
                }

                existingItem.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(existingItem.gameObject);
                }
                else
                {
                    DestroyImmediate(existingItem.gameObject);
                }
            }

            queueItems.Clear();
            if (ticketQueueContent != null)
            {
                ticketQueueContent.anchorMin = new Vector2(0f, 1f);
                ticketQueueContent.anchorMax = new Vector2(1f, 1f);
                ticketQueueContent.pivot = new Vector2(0.5f, 1f);
                ticketQueueContent.anchoredPosition = Vector2.zero;
                ticketQueueContent.sizeDelta = new Vector2(
                    0f,
                    Mathf.Max(0f, tickets.Count * 104f));
            }

            for (int index = 0; index < tickets.Count; index++)
            {
                Level1TicketQueueItemView item = Instantiate(ticketQueueItemTemplate, ticketQueueContent);
                RectTransform itemRect = item.GetComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0f, 1f);
                itemRect.anchorMax = Vector2.one;
                itemRect.pivot = new Vector2(0.5f, 1f);
                itemRect.sizeDelta = new Vector2(0f, 90f);
                itemRect.anchoredPosition = new Vector2(0f, -index * 104f);
                item.gameObject.name = $"QueueItem_{tickets[index].TicketId}";
                item.gameObject.SetActive(true);
                item.Configure(index, tickets[index], NotifyTicketSelected);
                queueItems.Add(item);
            }
        }

        /// <summary>
        /// 回到仅显示左侧工单列表的状态，等待玩家主动选择。
        /// </summary>
        public void ShowTicketSelection(
            string stageName,
            int processedCount,
            int totalCount,
            GameMetrics metrics,
            IReadOnlyList<bool> processedStates)
        {
            ticketProgressText.text =
                $"{stageName}  ·  已处理 {processedCount} / {totalCount}";
            statusText.text = "状态：请选择左侧工单";
            UpdateMetricHeader(metrics);
            ticketTitleText.text = "未知";
            ticketMetaText.text =
                "用户：未知    用户情绪：未知\n" +
                "区域：未知";
            SetTicketIdText("#XXXXXXXX");

            hasActiveTicketContent = false;
            SetTicketContentVisible(true);
            SetDataPanelVisible(false);
            SetNotebookPanelVisible(false);
            SetEvidenceDetailVisible(false);
            ShowEmptyChatState(true);
            ClearChatMessages();
            resultPanel.SetActive(false);
            resolveTutorialHintVisible = false;
            RestoreMarkResolvedHighlight();
            ResetEvidenceActionRows();
            SetFollowUpButtonState(false);
            if (dataLookupButton != null)
            {
                dataLookupButton.interactable = false;
            }
            if (transferHumanButton != null)
            {
                transferHumanButton.interactable = false;
            }
            if (saveEvidenceButton != null)
            {
                saveEvidenceButton.interactable = false;
            }
            if (markResolvedButton != null)
            {
                markResolvedButton.interactable = false;
            }
            SetEvidenceButtonsInteractable(false);
            RefreshQueueStates(-1, processedStates);
        }

        /// <summary>
        /// 显示一条新工单，并恢复到“先查看资料”的初始状态。
        /// </summary>
        public void ShowTicket(
            TicketData ticket,
            string stageName,
            int currentIndex,
            int totalCount,
            GameMetrics metrics)
        {
            ticketProgressText.text =
                $"{stageName}  ·  工单 {currentIndex} / {totalCount}  ·  {ticket.TicketId}";
            statusText.text = "状态：等待核验";
            UpdateMetricHeader(metrics);
            ticketTitleText.text = $"{ticket.IssueType}：{ticket.Title}";
            ticketMetaText.text =
                $"用户：{ticket.UserName}    用户情绪：愤怒\n" +
                $"区域：{ticket.Region}";
            SetTicketIdText($"#{ticket.TicketId}");
            if (ticket.InitialDialogueLines != null &&
                ticket.InitialDialogueLines.Length > 0)
            {
                PlayTicketDialogue(ticket.InitialDialogueLines, true);
            }
            else
            {
            ShowDialogueSequence(
                ("求助 " + ticket.UserName, ticket.UserMessage, true),
                ("明湾通 AI", ticket.AiReply, false));

            }

            hasActiveTicketContent = true;
            SetTicketAppWindowVisible(true);
            SetTicketContentVisible(true);
            SetDataPanelVisible(false);
            SetNotebookPanelVisible(false);
            SetEvidenceDetailVisible(false);
            ResetEvidenceActionRows();
            resultPanel.SetActive(false);
            resolveTutorialHintVisible = false;
            RestoreMarkResolvedHighlight();
            SetButtonLabel(primaryActionButtonText, "追问");
            SetButtonLabel(dataLookupButtonText, "资料库查找");
            SetButtonLabel(transferHumanButtonText, "转人工");
            SetButtonLabel(saveEvidenceButtonText, "保留证据");
            SetFollowUpButtonState(ticket.FollowUpLines.Length > 0);
            if (dataLookupButton != null)
            {
                dataLookupButton.interactable = true;
            }
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = true;
            ResetResolveHold();
            chatEvidenceActionButton.gameObject.SetActive(false);
            SetEvidenceButtonsInteractable(false);
        }

        /// <summary>
        /// 展开资料并允许玩家做最终选择。
        /// </summary>
        public void ShowData(TicketData ticket)
        {
            bool tutorialEvidenceLayout = ticket.TicketId == "T_S01_001";
            bool stagedEvidenceFlow =
                ticket.AllowDirectEvidenceSave && ticket.HasEvidence;

            profileText.text = ticket.ProfileText;
            historyText.text = ticket.HistoryText;
            deviceLogText.text = ticket.DeviceLogText;
            regionStatusText.text = ticket.RegionStatusText;

            hasActiveTicketContent = true;
            SetTicketAppWindowVisible(true);
            SetTicketContentVisible(true);
            SetDataPanelVisible(true);
            SetNotebookPanelVisible(false);
            SetEvidenceDetailVisible(false);
            ResetEvidenceActionRows();
            ConfigureEvidenceCardLayout(tutorialEvidenceLayout);
            if (stagedEvidenceFlow)
            {
                AddEvidenceRetentionHint(profileText);
                AddEvidenceRetentionHint(historyText);
                AddEvidenceRetentionHint(deviceLogText);
                AddEvidenceRetentionHint(regionStatusText);
            }

            statusText.text = "状态：请选择追问、转人工、保留证据或标记已解决";
            SetButtonLabel(primaryActionButtonText, "追问");
            SetFollowUpButtonState(ticket.FollowUpLines.Length > 0);
            if (dataLookupButton != null)
            {
                dataLookupButton.interactable = true;
            }
            transferHumanButton.interactable = true;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = true;
            ResetResolveHold();
            SetEvidenceButtonsInteractable(ticket.HasEvidence);
        }

        /// <summary>
        /// 在资料查看阶段记录玩家选中的证据候选项，等待点击“保留证据”确认。
        /// </summary>
        public void ShowEvidenceCandidateSelected(int evidenceIndex)
        {
            statusText.text =
                $"状态：已选择资料{evidenceIndex + 1:00}，可查看详情或收集此资料";
            for (int index = 0; index < evidenceActionRows.Length; index++)
            {
                SetEvidenceActionRowVisible(index, index == evidenceIndex);
            }

            if (saveEvidenceButton != null)
            {
                saveEvidenceButton.interactable = true;
            }
        }

        /// <summary>
        /// 教程工单没有证据选择时，引导玩家使用标记已解决。
        /// </summary>
        public void ShowEvidenceDetailOnlySelected(int evidenceIndex)
        {
            statusText.text =
                $"状态: 已选择资料{evidenceIndex + 1:00}，请点击“查看资料详情”并在详情中选择关键词";
            for (int index = 0; index < evidenceActionRows.Length; index++)
            {
                SetEvidenceActionRowVisible(index, index == evidenceIndex);
                if (index < evidenceCollectButtons.Length &&
                    evidenceCollectButtons[index] != null)
                {
                    evidenceCollectButtons[index].gameObject.SetActive(false);
                }
            }

            if (saveEvidenceButton != null)
            {
                saveEvidenceButton.interactable = false;
                saveEvidenceButton.gameObject.SetActive(false);
            }
        }

        public void SetEvidenceCollectionControlsVisible(bool isVisible)
        {
            if (saveEvidenceButton != null)
            {
                saveEvidenceButton.gameObject.SetActive(isVisible);
                saveEvidenceButton.interactable = false;
            }

            foreach (Button collectButton in evidenceCollectButtons)
            {
                if (collectButton != null)
                {
                    collectButton.gameObject.SetActive(isVisible);
                }
            }
        }

        public void SetEvidenceDetailFormatter(
            Func<int, string, string> formatter)
        {
            evidenceDetailFormatter = formatter;
        }

        public void RefreshEvidenceDetailBody()
        {
            if (evidenceDetailOverlay == null ||
                !evidenceDetailOverlay.activeSelf ||
                currentEvidenceDetailIndex < 0 ||
                evidenceDetailBodyText == null)
            {
                return;
            }

            string detailBody = GetEvidenceDetailBody(currentEvidenceDetailIndex);
            evidenceDetailBodyText.text = evidenceDetailFormatter != null
                ? evidenceDetailFormatter.Invoke(currentEvidenceDetailIndex, detailBody)
                : detailBody;
            ConfigureEvidenceDetailLinkHandler();
            StartCoroutine(RebuildEvidenceDetailNextFrame());
        }

        public void ShowResolutionHint(string actionText)
        {
            if (!string.IsNullOrWhiteSpace(actionText))
            {
                AppendChatMessage("系统提示", actionText, false);
            }

            statusText.text = "教程提示：请使用“标记已解决”完成工单";
            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = true;
            SetEvidenceButtonsInteractable(false);
        }

        /// <summary>
        /// 显示 A-07 的证据请求，并允许点击四份资料。
        /// </summary>
        public void ShowEvidenceSelection(
            TicketData ticket,
            string actionText,
            string evidencePrompt)
        {
            string actionSection = string.IsNullOrWhiteSpace(actionText)
                ? string.Empty
                : $"\n\n{actionText}";
            AppendChatMessage(
                "A07",
                $"{actionSection.Trim()}\n{evidencePrompt}".Trim(),
                false);
            statusText.text = "状态：请点击资料01～资料04，并选择“收集此资料”提交证据";
            AddEvidenceClickHint(profileText);
            AddEvidenceClickHint(historyText);
            AddEvidenceClickHint(deviceLogText);
            AddEvidenceClickHint(regionStatusText);
            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
            SetEvidenceButtonsInteractable(true);
        }

        /// <summary>
        /// 将追问后获得的补充台词追加到居民消息框。
        /// </summary>
        public void ShowResidentFollowUp(
            string speakerName,
            string followUpText,
            bool hasRemainingFollowUp)
        {
            if (string.IsNullOrWhiteSpace(followUpText))
            {
                return;
            }

            string safeSpeakerName = string.IsNullOrWhiteSpace(speakerName)
                ? "居民"
                : speakerName;
            AppendChatMessage(safeSpeakerName, followUpText, true);
            RefreshResidentMessageLayout();
            statusText.text = hasRemainingFollowUp
                ? "状态：居民仍有补充说明，可继续点击“追问”"
                : "状态：居民补充说明已全部显示";
            SetFollowUpButtonState(hasRemainingFollowUp);
        }

        /// <summary>
        /// 第二张教程工单追问结束后，说明无法完美满足所有诉求，并引导关闭工单。
        /// </summary>
        public void ShowResolveTutorialHint()
        {
            resolveTutorialHintVisible = true;
            resultTitleText.text = "教程提示";
            if (resultStatusText != null)
            {
                resultStatusText.text = "请确认后继续";
            }

            resultDescriptionText.text =
                "不是每次都能完美解决居民诉求。游戏居民可能态度不好，" +
                "也可能要求太高、很难解决。你可以按下这个按钮轻松解决。";
            resultMetricsText.text = "关闭提示后，“标记已解决”按钮会被高亮。";
            SetButtonLabel(resultActionButtonText, "我知道了");
            resultPanel.SetActive(true);
        }

        private void HighlightMarkResolvedButton()
        {
            ApplyMarkResolvedDefaultColors();
            markResolvedButton.interactable = true;
            statusText.text = "教程提示：请长按“标记已解决”";
        }

        private void ApplyMarkResolvedDefaultColors()
        {
            if (markResolvedButton == null)
            {
                return;
            }

            ColorBlock colors = markResolvedButton.colors;
            colors.normalColor = new Color(0.85f, 0.34f, 0.34f, 1f);
            colors.highlightedColor = new Color(0.92f, 0.40f, 0.40f, 1f);
            colors.pressedColor = new Color(0.68f, 0.22f, 0.22f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.48f, 0.24f, 0.24f, 0.68f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            markResolvedButton.colors = colors;
            defaultMarkResolvedColors = colors;

            Graphic targetGraphic = markResolvedButton.targetGraphic;
            if (targetGraphic != null)
            {
                targetGraphic.color = markResolvedButton.interactable
                    ? colors.normalColor
                    : colors.disabledColor;
            }
        }

        private void RestoreMarkResolvedHighlight()
        {
            ApplyMarkResolvedDefaultColors();
        }

        /// <summary>
        /// 教程中直接保留证据后刷新计数，但不结束当前工单。
        /// </summary>
        public void ShowDirectEvidenceSaved(
            int evidenceIndex,
            GameMetrics metrics)
        {
            UpdateMetricHeader(metrics);
            statusText.text =
                $"已收集资料{evidenceIndex + 1:00}，现在可点击“转人工”";
            ResetEvidenceActionRows();
            saveEvidenceButton.interactable = false;
            SetEvidenceButtonsInteractable(true);
        }

        /// <summary>
        /// 转人工后打开笔记面板，仅展示玩家已经在资料库中收集过的资料。
        /// </summary>
        public void ShowEvidenceNotebook(
            TicketData ticket,
            IReadOnlyCollection<int> retainedEvidenceIndices)
        {
            selectedNotebookEvidenceIndex = -1;
            SetNotebookSelectedEvidence(-1);
            SetNotebookPanelVisible(true);

            if (notebookReasonText != null)
            {
                notebookReasonText.text = $"{ticket.IssueType}：{ticket.Title}";
            }

            if (notebookUserText != null)
            {
                notebookUserText.text = $"用户：{ticket.UserName}";
            }

            if (notebookEmotionText != null)
            {
                notebookEmotionText.text = "用户情绪：愤怒";
            }

            if (notebookRegionText != null)
            {
                notebookRegionText.text = $"区域：{ticket.Region}";
            }

            if (notebookTicketIdText != null)
            {
                notebookTicketIdText.text = $"#{ticket.TicketId}";
            }

            for (int index = 0; index < NotebookEvidenceSlotCount; index++)
            {
                bool isRetained = ContainsEvidenceIndex(retainedEvidenceIndices, index);
                if (notebookEvidenceButtons != null &&
                    index < notebookEvidenceButtons.Length &&
                    notebookEvidenceButtons[index] != null)
                {
                    notebookEvidenceButtons[index].gameObject.SetActive(isRetained);
                    notebookEvidenceButtons[index].interactable = isRetained;
                }

                if (notebookEvidenceTexts != null &&
                    index < notebookEvidenceTexts.Length &&
                    notebookEvidenceTexts[index] != null)
                {
                    notebookEvidenceTexts[index].text = GetEvidenceSummary(ticket, index);
                }
            }

            if (notebookSubmitButton != null)
            {
                notebookSubmitButton.interactable = false;
            }

            statusText.text = "状态：请选择一份已收集资料并提交证据";
            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
            SetEvidenceButtonsInteractable(false);
            SetDataPanelVisible(false);
        }

        /// <summary>
        /// 玩家关闭笔记面板后回到资料核验界面。
        /// </summary>
        public void ShowSingleEvidenceNotebook(
            TicketData ticket,
            string evidenceText)
        {
            selectedNotebookEvidenceIndex = 0;
            SetNotebookSelectedEvidence(0);
            SetNotebookPanelVisible(true);

            if (notebookReasonText != null)
            {
                notebookReasonText.text = $"{ticket.IssueType}: {ticket.Title}";
            }

            if (notebookUserText != null)
            {
                notebookUserText.text = $"用户: {ticket.UserName}";
            }

            if (notebookEmotionText != null)
            {
                notebookEmotionText.text = "用户情绪: 待复核";
            }

            if (notebookRegionText != null)
            {
                notebookRegionText.text = $"区域: {ticket.Region}";
            }

            if (notebookTicketIdText != null)
            {
                notebookTicketIdText.text = $"#{ticket.TicketId}";
            }

            for (int index = 0; index < NotebookEvidenceSlotCount; index++)
            {
                bool isPrimaryEvidence = index == 0;
                if (notebookEvidenceButtons != null &&
                    index < notebookEvidenceButtons.Length &&
                    notebookEvidenceButtons[index] != null)
                {
                    notebookEvidenceButtons[index].gameObject.SetActive(isPrimaryEvidence);
                    notebookEvidenceButtons[index].interactable = isPrimaryEvidence;
                }

                if (notebookEvidenceTexts != null &&
                    index < notebookEvidenceTexts.Length &&
                    notebookEvidenceTexts[index] != null)
                {
                    notebookEvidenceTexts[index].text = isPrimaryEvidence
                        ? $"AI建议证据\n{evidenceText}"
                        : string.Empty;
                }
            }

            if (notebookSubmitButton != null)
            {
                notebookSubmitButton.interactable = true;
            }

            statusText.text = "状态: 已生成 AI 建议证据，可提交证据";
            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
            SetEvidenceButtonsInteractable(false);
            SetDataPanelVisible(false);
        }

        public void HideEvidenceNotebook()
        {
            selectedNotebookEvidenceIndex = -1;
            SetNotebookSelectedEvidence(-1);
            SetNotebookPanelVisible(false);
        }

        /// <summary>
        /// 尚未保留证据时阻止进入人工核验阶段。
        /// </summary>
        public void ShowRetainedEvidenceRequired()
        {
            statusText.text = "请先点击一份资料并选择“收集此资料”，再转人工";
        }

        /// <summary>
        /// 转人工后在聊天区域等待玩家主动点击“出示证据”。
        /// </summary>
        public void ShowTransferHumanIntro()
        {
            AppendChatMessage(
                "\u660e\u6e7e\u901a AI",
                "\u5df2\u8f6c\u4eba\u5de5\u90e8\u95e8\u7ee7\u7eed\u5904\u7406",
                false);
            AppendChatMessage(
                "\u5ba2\u670d A-07",
                "\u60a8\u597d\uff0c\u300a\u660e\u6e7e\u901a\u300bA07\u5ba2\u670d\u5728\u7ebf\u4e3a\u60a8\u670d\u52a1",
                false);
        }

        public void ShowSubmittedEvidenceCustomerFeedback(string userReply)
        {
            string safeUserReply = string.IsNullOrWhiteSpace(userReply)
                ? "\u7528\u6237\u5df2\u6536\u5230\u5904\u7406\u7ed3\u679c\u3002"
                : userReply;
            AppendChatMessage(
                "\u5ba2\u6237\u53cd\u9988",
                safeUserReply,
                true);
            statusText.text =
                "\u72b6\u6001\uff1a\u5ba2\u6237\u5df2\u53cd\u9988\uff0c\u6b63\u5728\u751f\u6210\u7ed3\u7b97";
        }

        public void ShowEvidencePresentationRequest(
            TicketData ticket,
            string actionText,
            string evidencePrompt)
        {
            AppendChatMessage(
                "A07",
                $"{actionText}\n{evidencePrompt}".Trim(),
                false);
            statusText.text = "状态：人工客服正在等待证据";
            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
            SetEvidenceButtonsInteractable(false);
            SetButtonLabel(chatEvidenceActionButtonText, "出示证据");
            chatEvidenceActionButton.gameObject.SetActive(true);
            chatEvidenceActionButton.interactable = true;
        }

        /// <summary>
        /// 出示已保留证据后先展示客服与用户回应，再允许玩家进入结算。
        /// </summary>
        public void ShowEvidenceDialogue(
            int evidenceIndex,
            string userReply)
        {
            string safeUserReply = string.IsNullOrWhiteSpace(userReply)
                ? "用户：我明白了，请按这个结果继续处理。"
                : userReply;
            AppendChatMessage("玩家", $"出示资料{evidenceIndex + 1:00}", false);
            AppendChatMessage("A07", "已收到您出示的证据，正在核验相关记录。", false);
            AppendChatMessage("居民回应", safeUserReply, true);
            RefreshResidentMessageLayout();
            statusText.text = "状态：用户已回应，请完成本次对话";
            SetButtonLabel(chatEvidenceActionButtonText, "完成对话");
            chatEvidenceActionButton.interactable = true;
        }

        /// <summary>
        /// 教程证据核验正确后返回处理界面，等待玩家主动标记已解决。
        /// </summary>
        public void ShowTutorialEvidenceAccepted(string feedbackText, GameMetrics metrics)
        {
            UpdateMetricHeader(metrics);
            AppendChatMessage("A07", feedbackText, false);
            statusText.text = "教程提示：证据核验正确，请点击“标记已解决”";
            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = true;
            SetEvidenceButtonsInteractable(false);
        }

        /// <summary>
        /// 教程证据核验错误时保留证据选择状态，允许重新点击资料。
        /// </summary>
        public void ShowTutorialEvidenceRejected(string feedbackText, GameMetrics metrics)
        {
            UpdateMetricHeader(metrics);
            AppendChatMessage("A07", feedbackText, false);
            statusText.text = "教程提示：证据不匹配，请重新点击资料01～资料04";
            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
            SetEvidenceButtonsInteractable(true);
        }

        /// <summary>
        /// 锁定操作并显示玩家选择造成的结果。
        /// </summary>
        public void ShowResult(
            string resultTitle,
            string resultDescription,
            bool evidenceSaved,
            GameMetrics metrics,
            string resultActionLabel)
        {
            statusText.text = "状态：处理完成";
            UpdateMetricHeader(metrics);
            resultTitleText.text = resultTitle;
            if (resultStatusText != null)
            {
                resultStatusText.text = "工单已关闭";
            }

            resultDescriptionText.text = resultDescription;
            resultMetricsText.text =
                $"A07风险值{metrics.A07Risk:+#;-#;0}\n" +
                $"已解决数量：{metrics.ResolvedCount}\n" +
                $"本次证据记录：{(evidenceSaved ? "已" : "无")}";
            if (resultActionButtonText != null)
            {
                resultActionButtonText.text = "点击任意空白位置关闭";
            }

            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
            ResetResolveHold();
            chatEvidenceActionButton.gameObject.SetActive(false);
            SetEvidenceButtonsInteractable(false);
            ResetEvidenceActionRows();
            SetDataPanelVisible(false);
            SetNotebookPanelVisible(false);
            RepositionResultCard();
            resultPanel.SetActive(true);
        }

        /// <summary>
        /// 根据流程保存的处理状态统一刷新队列。
        /// </summary>
        public void RefreshQueueStates(int selectedIndex, IReadOnlyList<bool> processedStates)
        {
            for (int index = 0; index < queueItems.Count; index++)
            {
                bool isProcessed = processedStates != null &&
                                   index < processedStates.Count &&
                                   processedStates[index];
                queueItems[index].SetState(index == selectedIndex, isProcessed);
            }
        }

        private void UpdateMetricHeader(GameMetrics metrics)
        {
            evidenceCountText.text = $"证据  {metrics.EvidenceCount}";
            resolvedCountText.text = $"已解决  {metrics.ResolvedCount}";
        }

        private void SetTicketIdText(string content)
        {
            if (ticketIdText != null)
            {
                ticketIdText.text = content;
            }
        }

        private void RepositionResultCard()
        {
            if (resultPanel == null)
            {
                return;
            }

            Transform card = resultPanel.transform.Find("ResultCard");
            if (card == null ||
                !card.TryGetComponent(out RectTransform rect))
            {
                return;
            }

            rect.anchorMin = new Vector2(0.33f, 0.46f);
            rect.anchorMax = new Vector2(0.67f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void CacheButtonLabels()
        {
            primaryActionButtonText =
                primaryActionButton != null
                    ? primaryActionButton.GetComponentInChildren<TMP_Text>(true)
                    : null;
            transferHumanButtonText =
                transferHumanButton != null
                    ? transferHumanButton.GetComponentInChildren<TMP_Text>(true)
                    : null;
            saveEvidenceButtonText =
                saveEvidenceButton != null
                    ? saveEvidenceButton.GetComponentInChildren<TMP_Text>(true)
                    : null;
            dataLookupButtonText =
                dataLookupButton != null
                    ? dataLookupButton.GetComponentInChildren<TMP_Text>(true)
                    : null;
            chatEvidenceActionButtonText =
                chatEvidenceActionButton != null
                    ? chatEvidenceActionButton.GetComponentInChildren<TMP_Text>(true)
                    : null;
            resultActionButtonText =
                resultActionButton != null
                    ? resultActionButton.GetComponentInChildren<TMP_Text>(true)
                    : null;
        }

        /// <summary>
        /// 居民框会依次显示原始求助、追问和证据回应，需要允许字号随内容自动缩小。
        /// </summary>
        private void ConfigureConversationText()
        {
            if (userMessageText == null)
            {
                return;
            }

            userMessageText.enableAutoSizing = true;
            userMessageText.fontSizeMin = 8f;
            userMessageText.fontSizeMax = 18f;
            userMessageText.lineSpacing = -5f;
            userMessageText.overflowMode = TextOverflowModes.Truncate;

            RectTransform textRect = userMessageText.rectTransform;
            textRect.anchorMin = new Vector2(0.12f, 0.03f);
            textRect.anchorMax = new Vector2(0.97f, 0.82f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void RefreshResidentMessageLayout()
        {
            if (userMessageText == null)
            {
                return;
            }

            userMessageText.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                userMessageText.transform.parent as RectTransform);
            userMessageText.ForceMeshUpdate(true, true);
        }

        public void PlayTicketDialogue(
            IReadOnlyList<TicketDialogueLine> lines,
            bool clearExisting,
            Action onComplete = null)
        {
            List<(string speaker, string message, bool fromUser)> runtimeLines = new();
            if (lines != null)
            {
                foreach (TicketDialogueLine line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line.Text))
                    {
                        continue;
                    }

                    string speaker = string.IsNullOrWhiteSpace(line.SpeakerLabel)
                        ? line.SpeakerId
                        : line.SpeakerLabel;
                    runtimeLines.Add((speaker, line.Text, line.FromUser));
                }
            }

            ShowDialogueSequence(clearExisting, onComplete, runtimeLines.ToArray());
        }

        private void ShowDialogueSequence(params (string speaker, string message, bool fromUser)[] lines)
        {
            ShowDialogueSequence(true, null, lines);
        }

        private void ShowDialogueSequence(
            bool clearExisting,
            Action onComplete,
            params (string speaker, string message, bool fromUser)[] lines)
        {
            if (dialogueRoutine != null)
            {
                StopCoroutine(dialogueRoutine);
            }

            if (clearExisting)
            {
                ClearChatMessages();
            }

            ShowEmptyChatState(false);
            if (lines == null || lines.Length == 0)
            {
                if (onComplete != null)
                {
                    dialogueRoutine = StartCoroutine(CompleteAfterPopupDelay(onComplete));
                    return;
                }

                onComplete?.Invoke();
                return;
            }

            dialogueRoutine = StartCoroutine(RevealDialogueRoutine(lines, onComplete));
        }

        private IEnumerator RevealDialogueRoutine(
            (string speaker, string message, bool fromUser)[] lines,
            Action onComplete)
        {
            for (int index = 0; index < lines.Length; index++)
            {
                AppendChatMessage(lines[index].speaker, lines[index].message, lines[index].fromUser);
                if (index < lines.Length - 1)
                {
                    yield return new WaitForSeconds(dialogueRevealDelay);
                }
            }

            if (onComplete != null && popupDelayAfterDialogueSeconds > 0f)
            {
                yield return new WaitForSeconds(popupDelayAfterDialogueSeconds);
            }

            dialogueRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator CompleteAfterPopupDelay(Action onComplete)
        {
            if (popupDelayAfterDialogueSeconds > 0f)
            {
                yield return new WaitForSeconds(popupDelayAfterDialogueSeconds);
            }

            dialogueRoutine = null;
            onComplete?.Invoke();
        }

        private void ClearChatMessages()
        {
            if (chatContent == null || chatBubbleTemplate == null)
            {
                if (userMessageText != null)
                {
                    userMessageText.text = string.Empty;
                }

                if (aiReplyText != null)
                {
                    aiReplyText.text = string.Empty;
                }

                return;
            }

            for (int index = chatContent.childCount - 1; index >= 0; index--)
            {
                Transform child = chatContent.GetChild(index);
                if (child.gameObject == chatBubbleTemplate)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        private void AppendChatMessage(string speaker, string message, bool fromUser)
        {
            ShowEmptyChatState(false);
            if (chatContent == null || chatBubbleTemplate == null)
            {
                TMP_Text target = fromUser ? userMessageText : aiReplyText;
                if (target != null)
                {
                    target.text = string.IsNullOrWhiteSpace(target.text)
                        ? message
                        : $"{target.text}\n\n{message}";
                }

                return;
            }

            GameObject bubble = Instantiate(chatBubbleTemplate, chatContent);
            bubble.name = fromUser ? "Bubble_User" : "Bubble_System";
            bubble.SetActive(true);

            RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
            LayoutElement layoutElement = bubble.GetComponent<LayoutElement>();
            float preferredHeight = CalculateChatBubbleHeight(message);
            if (layoutElement != null)
            {
                layoutElement.preferredHeight = preferredHeight;
            }

            if (bubbleRect != null)
            {
                bubbleRect.sizeDelta = new Vector2(bubbleRect.sizeDelta.x, preferredHeight);
            }

            RectTransform bodyRect = FindChildRect(bubble.transform, "BubbleBody");
            Image bubbleImage = bodyRect != null
                ? bodyRect.GetComponent<Image>()
                : bubble.GetComponent<Image>();
            if (bubbleImage != null)
            {
                bubbleImage.color = fromUser
                    ? new Color(0.96f, 0.96f, 0.96f, 1f)
                    : new Color(0.32f, 0.52f, 0.26f, 1f);
            }

            RectTransform leftAvatar = FindChildRect(bubble.transform, "AvatarLeft");
            RectTransform rightAvatar = FindChildRect(bubble.transform, "AvatarRight");
            TMP_Text speakerText = FindChildText(bubble.transform, "Txt_Speaker");
            TMP_Text text = FindChildText(bubble.transform, "Txt_Bubble") ??
                bubble.GetComponentInChildren<TMP_Text>(true);

            ConfigureChatBubbleLayout(
                fromUser,
                preferredHeight,
                bodyRect,
                leftAvatar,
                rightAvatar,
                speakerText);

            if (speakerText != null)
            {
                speakerText.text = speaker;
                speakerText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            }

            if (text != null)
            {
                text.text = speakerText != null ? message : $"<b>{speaker}</b>\n{message}";
                text.enableAutoSizing = false;
                text.fontSize = 20f;
                text.overflowMode = TextOverflowModes.Overflow;
                text.color = fromUser
                    ? new Color(0.08f, 0.08f, 0.08f, 1f)
                    : new Color(0.02f, 0.02f, 0.02f, 1f);
            }

            StartCoroutine(ScrollChatToBottomNextFrame());
        }

        private static float CalculateChatBubbleHeight(string message)
        {
            int length = string.IsNullOrEmpty(message) ? 0 : message.Length;
            int lineBreaks = string.IsNullOrEmpty(message)
                ? 0
                : message.Split('\n').Length - 1;
            return Mathf.Clamp(108f + Mathf.Ceil(length / 34f) * 26f + lineBreaks * 22f, 132f, 300f);
        }

        private static void ConfigureChatBubbleLayout(
            bool fromUser,
            float rowHeight,
            RectTransform bodyRect,
            RectTransform leftAvatar,
            RectTransform rightAvatar,
            TMP_Text speakerText)
        {
            const float avatarSize = 58f;
            const float bodyWidth = 650f;
            float bodyHeight = Mathf.Max(72f, rowHeight - 58f);
            Vector2 leftBodyPosition = new(96f, -46f);
            Vector2 rightBodyPosition = new(-96f, -46f);

            if (leftAvatar != null)
            {
                leftAvatar.gameObject.SetActive(fromUser);
                leftAvatar.anchorMin = new Vector2(0f, 1f);
                leftAvatar.anchorMax = new Vector2(0f, 1f);
                leftAvatar.pivot = new Vector2(0f, 1f);
                leftAvatar.sizeDelta = new Vector2(avatarSize, avatarSize);
                leftAvatar.anchoredPosition = new Vector2(16f, -34f);
            }

            if (rightAvatar != null)
            {
                rightAvatar.gameObject.SetActive(!fromUser);
                rightAvatar.anchorMin = new Vector2(1f, 1f);
                rightAvatar.anchorMax = new Vector2(1f, 1f);
                rightAvatar.pivot = new Vector2(1f, 1f);
                rightAvatar.sizeDelta = new Vector2(avatarSize, avatarSize);
                rightAvatar.anchoredPosition = new Vector2(-16f, -34f);
            }

            if (bodyRect != null)
            {
                bodyRect.anchorMin = fromUser ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                bodyRect.anchorMax = bodyRect.anchorMin;
                bodyRect.pivot = fromUser ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                bodyRect.sizeDelta = new Vector2(bodyWidth, bodyHeight);
                bodyRect.anchoredPosition = fromUser ? leftBodyPosition : rightBodyPosition;
            }

            if (speakerText != null)
            {
                RectTransform speakerRect = speakerText.GetComponent<RectTransform>();
                speakerRect.anchorMin = fromUser ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                speakerRect.anchorMax = speakerRect.anchorMin;
                speakerRect.pivot = fromUser ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                speakerRect.sizeDelta = new Vector2(280f, 28f);
                speakerRect.anchoredPosition = fromUser
                    ? new Vector2(96f, -8f)
                    : new Vector2(-96f, -8f);
                speakerText.alignment = fromUser
                    ? TextAlignmentOptions.MidlineLeft
                    : TextAlignmentOptions.MidlineRight;
            }
        }

        private static RectTransform FindChildRect(Transform parent, string childName)
        {
            Transform child = FindChild(parent, childName);
            return child != null ? child.GetComponent<RectTransform>() : null;
        }

        private static TMP_Text FindChildText(Transform parent, string childName)
        {
            Transform child = FindChild(parent, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private void ShowEmptyChatState(bool isVisible)
        {
            if (chatEmptyState != null)
            {
                chatEmptyState.SetActive(isVisible);
            }
        }

        private IEnumerator ScrollChatToBottomNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (chatContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent);
            }

            if (chatScrollRect != null)
            {
                chatScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void SetFollowUpButtonState(bool hasFollowUp)
        {
            if (primaryActionButton == null)
            {
                return;
            }

            primaryActionButton.interactable = hasFollowUp;
            ColorBlock colors = primaryActionButton.colors;
            if (hasFollowUp)
            {
                colors.normalColor = new Color(0.2f, 0.68f, 0.42f, 1f);
                colors.highlightedColor = new Color(0.26f, 0.78f, 0.5f, 1f);
                colors.pressedColor = new Color(0.16f, 0.48f, 0.31f, 1f);
            }
            else
            {
                colors.normalColor = new Color(0.45f, 0.45f, 0.45f, 1f);
                colors.highlightedColor = colors.normalColor;
                colors.pressedColor = colors.normalColor;
            }

            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.72f);
            primaryActionButton.colors = colors;
        }

        private void ConfigureLongPressButton(Button button)
        {
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers ??= new List<EventTrigger.Entry>();
            trigger.triggers.Clear();

            EventTrigger.Entry down = new() { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => BeginResolveHold());
            trigger.triggers.Add(down);

            EventTrigger.Entry up = new() { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => CancelResolveHold());
            trigger.triggers.Add(up);

            EventTrigger.Entry exit = new() { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => CancelResolveHold());
            trigger.triggers.Add(exit);
        }

        private void BeginResolveHold()
        {
            if (markResolvedButton == null || !markResolvedButton.interactable)
            {
                return;
            }

            isHoldingMarkResolved = true;
            markResolvedHoldTriggered = false;
            markResolvedHoldTimer = 0f;
            SetResolveHoldFill(0f);
        }

        private void CancelResolveHold()
        {
            if (markResolvedHoldTriggered)
            {
                return;
            }

            isHoldingMarkResolved = false;
            markResolvedHoldTimer = 0f;
            SetResolveHoldFill(0f);
        }

        private void ResetResolveHold()
        {
            isHoldingMarkResolved = false;
            markResolvedHoldTriggered = false;
            markResolvedHoldTimer = 0f;
            SetResolveHoldFill(0f);
        }

        private void SetResolveHoldFill(float fillAmount)
        {
            if (markResolvedHoldFill != null)
            {
                markResolvedHoldFill.fillAmount = fillAmount;
            }
        }

        private void AddNotebookEvidenceListeners()
        {
            if (notebookEvidenceButtons == null)
            {
                return;
            }

            if (notebookEvidenceButtons.Length > 0 && notebookEvidenceButtons[0] != null)
            {
                notebookEvidenceButtons[0].onClick.AddListener(NotifyNotebookEvidence01Selected);
            }

            if (notebookEvidenceButtons.Length > 1 && notebookEvidenceButtons[1] != null)
            {
                notebookEvidenceButtons[1].onClick.AddListener(NotifyNotebookEvidence02Selected);
            }

            if (notebookEvidenceButtons.Length > 2 && notebookEvidenceButtons[2] != null)
            {
                notebookEvidenceButtons[2].onClick.AddListener(NotifyNotebookEvidence03Selected);
            }

            if (notebookEvidenceButtons.Length > 3 && notebookEvidenceButtons[3] != null)
            {
                notebookEvidenceButtons[3].onClick.AddListener(NotifyNotebookEvidence04Selected);
            }
        }

        private void RemoveNotebookEvidenceListeners()
        {
            if (notebookEvidenceButtons == null)
            {
                return;
            }

            if (notebookEvidenceButtons.Length > 0 && notebookEvidenceButtons[0] != null)
            {
                notebookEvidenceButtons[0].onClick.RemoveListener(NotifyNotebookEvidence01Selected);
            }

            if (notebookEvidenceButtons.Length > 1 && notebookEvidenceButtons[1] != null)
            {
                notebookEvidenceButtons[1].onClick.RemoveListener(NotifyNotebookEvidence02Selected);
            }

            if (notebookEvidenceButtons.Length > 2 && notebookEvidenceButtons[2] != null)
            {
                notebookEvidenceButtons[2].onClick.RemoveListener(NotifyNotebookEvidence03Selected);
            }

            if (notebookEvidenceButtons.Length > 3 && notebookEvidenceButtons[3] != null)
            {
                notebookEvidenceButtons[3].onClick.RemoveListener(NotifyNotebookEvidence04Selected);
            }
        }

        private void SelectNotebookEvidence(int evidenceIndex)
        {
            if (notebookPanel == null || !notebookPanel.activeSelf)
            {
                return;
            }

            if (notebookEvidenceButtons == null ||
                evidenceIndex < 0 ||
                evidenceIndex >= notebookEvidenceButtons.Length ||
                notebookEvidenceButtons[evidenceIndex] == null ||
                !notebookEvidenceButtons[evidenceIndex].gameObject.activeSelf)
            {
                return;
            }

            selectedNotebookEvidenceIndex = evidenceIndex;
            SetNotebookSelectedEvidence(evidenceIndex);
            if (notebookSubmitButton != null)
            {
                notebookSubmitButton.interactable = true;
            }

            EvidenceSelected?.Invoke(evidenceIndex);
        }

        private void SetNotebookSelectedEvidence(int evidenceIndex)
        {
            if (notebookEvidenceOutlines == null)
            {
                return;
            }

            for (int index = 0; index < notebookEvidenceOutlines.Length; index++)
            {
                if (notebookEvidenceOutlines[index] != null)
                {
                    notebookEvidenceOutlines[index].enabled = index == evidenceIndex;
                }
            }
        }

        private void SetNotebookPanelVisible(bool isVisible)
        {
            if (notebookPanel != null)
            {
                notebookPanel.SetActive(isVisible);
            }
        }

        private static bool ContainsEvidenceIndex(
            IReadOnlyCollection<int> evidenceIndices,
            int evidenceIndex)
        {
            if (evidenceIndices == null)
            {
                return false;
            }

            foreach (int retainedIndex in evidenceIndices)
            {
                if (retainedIndex == evidenceIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetEvidenceSummary(TicketData ticket, int evidenceIndex)
        {
            string body = evidenceIndex switch
            {
                0 => ticket.ProfileText,
                1 => ticket.HistoryText,
                2 => ticket.DeviceLogText,
                3 => ticket.RegionStatusText,
                _ => string.Empty
            };

            string normalized = string.IsNullOrWhiteSpace(body)
                ? "暂无资料"
                : body.Replace("\r", string.Empty).Trim();
            string[] lines = normalized.Split('\n');
            string firstLine = lines.Length > 0 ? lines[0].Trim() : normalized;
            string secondLine = lines.Length > 1 ? lines[1].Trim() : string.Empty;
            string summary = string.IsNullOrWhiteSpace(secondLine)
                ? firstLine
                : $"{firstLine}\n{secondLine}";
            return $"资料{evidenceIndex + 1:00}:\n{summary}";
        }

        private static void SetButtonLabel(TMP_Text label, string content)
        {
            if (label != null)
            {
                label.text = content;
            }
        }

        private static void AddEvidenceClickHint(TMP_Text evidenceText)
        {
        }

        private static void AddEvidenceRetentionHint(TMP_Text evidenceText)
        {
        }

        private void EnsureEvidenceButtons()
        {
            TMP_Text[] evidenceTexts =
            {
                profileText,
                historyText,
                deviceLogText,
                regionStatusText
            };

            for (int index = 0; index < evidenceTexts.Length; index++)
            {
                TMP_Text evidenceText = evidenceTexts[index];
                if (evidenceText == null || evidenceText.transform.parent == null)
                {
                    continue;
                }

                evidenceText.enableAutoSizing = false;
                evidenceText.fontSize = 18f;

                GameObject cardObject = evidenceText.transform.parent.gameObject;
                Button button = cardObject.GetComponent<Button>();
                if (button == null)
                {
                    button = cardObject.AddComponent<Button>();
                }

                button.targetGraphic = cardObject.GetComponent<Image>();
                button.interactable = false;
                evidenceButtons[index] = button;
                evidenceDetailButtons[index] =
                    FindChildButton(cardObject.transform, "Btn_ViewDetail");
                evidenceCollectButtons[index] =
                    FindChildButton(cardObject.transform, "Btn_CollectEvidence");
                Transform actionRow = cardObject.transform.Find("ActionRow");
                evidenceActionRows[index] = actionRow != null
                    ? actionRow.gameObject
                    : null;
                evidenceCardLayouts[index] = cardObject.GetComponent<LayoutElement>();
            }

            for (int index = 0; index < evidenceButtons.Length; index++)
            {
                int evidenceIndex = index;
                if (evidenceButtons[index] != null)
                {
                    evidenceButtons[index].onClick.AddListener(
                        () => NotifyEvidenceSelected(evidenceIndex));
                }

                if (evidenceDetailButtons[index] != null)
                {
                    evidenceDetailButtons[index].onClick.AddListener(
                        () => ShowEvidenceDetail(evidenceIndex));
                }

                if (evidenceCollectButtons[index] != null)
                {
                    evidenceCollectButtons[index].onClick.AddListener(
                        NotifySaveEvidenceRequested);
                }

                SetEvidenceActionRowVisible(index, false);
            }
        }

        private void SetEvidenceButtonsInteractable(bool interactable)
        {
            foreach (Button evidenceButton in evidenceButtons)
            {
                if (evidenceButton != null)
                {
                    evidenceButton.interactable = interactable;
                }
            }
        }

        private void ResetEvidenceActionRows()
        {
            for (int index = 0; index < evidenceActionRows.Length; index++)
            {
                SetEvidenceActionRowVisible(index, false);
            }
        }

        private void SetEvidenceActionRowVisible(int evidenceIndex, bool isVisible)
        {
            if (evidenceIndex < 0 || evidenceIndex >= evidenceActionRows.Length)
            {
                return;
            }

            if (evidenceActionRows[evidenceIndex] != null)
            {
                evidenceActionRows[evidenceIndex].SetActive(isVisible);
            }

            if (evidenceCardLayouts[evidenceIndex] != null)
            {
                evidenceCardLayouts[evidenceIndex].preferredHeight = isVisible ? 300f : 220f;
            }

            ConfigureEvidenceCardTextLayout(GetEvidenceText(evidenceIndex), isVisible);
        }

        private void ShowEvidenceDetail(int evidenceIndex)
        {
            statusText.text = $"状态：正在查看资料{evidenceIndex + 1:00}详情";
            SetEvidenceActionRowVisible(evidenceIndex, true);
            ShowEvidenceDetailOverlay(evidenceIndex);
        }

        private void ShowEvidenceDetailOverlay(int evidenceIndex)
        {
            EnsureEvidenceDetailReferences();
            if (evidenceDetailOverlay == null)
            {
                return;
            }

            currentEvidenceDetailIndex = evidenceIndex;
            if (evidenceDetailTitleText != null)
            {
                evidenceDetailTitleText.text = $"\u8d44\u6599{evidenceIndex + 1:00}";
            }

            if (evidenceDetailBodyText != null)
            {
                string detailBody = GetEvidenceDetailBody(evidenceIndex);
                evidenceDetailBodyText.text = evidenceDetailFormatter != null
                    ? evidenceDetailFormatter.Invoke(evidenceIndex, detailBody)
                    : detailBody;
                evidenceDetailBodyText.enableAutoSizing = false;
                evidenceDetailBodyText.fontSize = 22f;
                evidenceDetailBodyText.overflowMode = TextOverflowModes.Overflow;
                evidenceDetailBodyText.raycastTarget = true;
                ConfigureEvidenceDetailLinkHandler();
            }

            evidenceDetailOverlay.SetActive(true);
            evidenceDetailOverlay.transform.SetAsLastSibling();
            EvidenceDetailOpened?.Invoke(evidenceIndex);
            StartCoroutine(RebuildEvidenceDetailNextFrame());
        }

        private void ConfigureEvidenceDetailLinkHandler()
        {
            if (evidenceDetailBodyText == null)
            {
                return;
            }

            RegisterEvidenceDetailLinkTarget(
                evidenceDetailBodyText.gameObject,
                false);
            RegisterEvidenceDetailLinkTarget(
                evidenceDetailBodyText.transform.parent != null
                    ? evidenceDetailBodyText.transform.parent.gameObject
                    : null,
                true);
            RegisterEvidenceDetailLinkTarget(
                evidenceDetailBodyText.transform.parent != null &&
                evidenceDetailBodyText.transform.parent.parent != null
                    ? evidenceDetailBodyText.transform.parent.parent.gameObject
                    : null,
                true);
        }

        private void RegisterEvidenceDetailLinkTarget(
            GameObject targetObject,
            bool ensureRaycastTarget)
        {
            if (targetObject == null)
            {
                return;
            }

            if (ensureRaycastTarget)
            {
                EnsureEvidenceDetailRaycastTarget(targetObject);
            }

            EvidenceDetailLinkHandler handler =
                targetObject.GetComponent<EvidenceDetailLinkHandler>();
            if (handler == null)
            {
                handler = targetObject.AddComponent<EvidenceDetailLinkHandler>();
            }

            handler.Configure(
                evidenceDetailBodyText,
                NotifyEvidenceDetailKeywordClicked);
            if (!evidenceDetailLinkHandlers.Contains(handler))
            {
                evidenceDetailLinkHandlers.Add(handler);
            }
        }

        private static void EnsureEvidenceDetailRaycastTarget(GameObject targetObject)
        {
            Graphic graphic = targetObject.GetComponent<Graphic>();
            if (graphic == null)
            {
                Image image = targetObject.AddComponent<Image>();
                image.color = Color.clear;
                graphic = image;
            }

            graphic.raycastTarget = true;
        }

        private IEnumerator RebuildEvidenceDetailNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (evidenceDetailBodyText != null &&
                evidenceDetailBodyText.transform.parent is RectTransform content)
            {
                float preferredHeight = Mathf.Max(
                    420f,
                    evidenceDetailBodyText.preferredHeight + 18f);
                content.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    preferredHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
        }

        private string GetEvidenceDetailBody(int evidenceIndex)
        {
            TMP_Text bodyText = evidenceIndex switch
            {
                0 => profileText,
                1 => historyText,
                2 => deviceLogText,
                3 => regionStatusText,
                _ => null
            };

            return bodyText != null && !string.IsNullOrWhiteSpace(bodyText.text)
                ? bodyText.text.Trim()
                : "\u6682\u65e0\u8d44\u6599";
        }

        private void CloseEvidenceDetail()
        {
            currentEvidenceDetailIndex = -1;
            SetEvidenceDetailVisible(false);
        }

        private void SetEvidenceDetailVisible(bool isVisible)
        {
            if (evidenceDetailOverlay != null)
            {
                evidenceDetailOverlay.SetActive(isVisible);
            }
        }

        private void OpenWorkApp()
        {
            SetTicketAppWindowVisible(true);
            SetTicketContentVisible(true);
            if (!hasActiveTicketContent)
            {
                SetDataPanelVisible(false);
            }
        }

        private void CloseWorkApp()
        {
            SetTicketAppWindowVisible(false);
        }

        private void OpenClueNotebook()
        {
            SetTicketAppWindowVisible(false);
            SetDataPanelVisible(false);
        }

        private void SetTicketAppWindowVisible(bool isVisible)
        {
            if (ticketAppWindow != null)
            {
                ticketAppWindow.SetActive(isVisible);
            }
        }

        private void SetTicketContentVisible(bool isVisible)
        {
            if (ticketContentObjects == null)
            {
                return;
            }

            foreach (GameObject contentObject in ticketContentObjects)
            {
                if (contentObject != null)
                {
                    contentObject.SetActive(isVisible);
                }
            }
        }

        private void SetDataPanelVisible(bool isVisible)
        {
            if (dataPanel != null)
            {
                dataPanel.SetActive(isVisible);
            }

            if (!isVisible)
            {
                SetEvidenceDetailVisible(false);
            }

            if (isVisible && dataScrollRect != null)
            {
                StartCoroutine(ScrollDataPanelToTopNextFrame());
            }
        }

        private IEnumerator ScrollDataPanelToTopNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (dataScrollRect != null)
            {
                dataScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// 兼容尚未重新生成的旧 Level1Scene。
        /// 通过对象名称复用旧的队列占位项，不要求策划手动重新绑定 Inspector。
        /// </summary>
        private void EnsureRuntimeReferences()
        {
            if (ticketAppWindow == null)
            {
                ticketAppWindow = FindSceneObject("TicketAppWindow");
            }

            if (workAppButton == null)
            {
                workAppButton = FindButton("DesktopButton_WorkApp");
            }

            if (ticketAppCloseButton == null)
            {
                ticketAppCloseButton = FindButton("Btn_TicketAppClose");
            }

            if (clueNotebookButton == null)
            {
                clueNotebookButton = FindButton("DesktopButton_ClueNotebook");
            }

            if (taskbarWorkQueueButton == null)
            {
                taskbarWorkQueueButton = FindButton("Taskbar_WorkQueue");
            }

            if (taskbarDatabaseButton == null)
            {
                taskbarDatabaseButton = FindButton("Taskbar_Database");
            }

            if (dataPanel == null)
            {
                dataPanel = FindSceneObject("EvidenceLibraryPanel");
            }

            if (dataScrollRect == null && dataPanel != null)
            {
                dataScrollRect = dataPanel.GetComponent<ScrollRect>();
            }

            EnsureEvidenceDetailReferences();

            if (chatEmptyState == null)
            {
                chatEmptyState = FindSceneObject("ChatEmptyState");
            }

            if (ticketIdText == null)
            {
                GameObject ticketIdObject = FindSceneObject("Txt_TicketId");
                ticketIdText = ticketIdObject != null
                    ? ticketIdObject.GetComponent<TMP_Text>()
                    : null;
            }

            if (ticketQueueContent == null)
            {
                GameObject queueContentObject = FindSceneObject("TicketQueueContent");
                ticketQueueContent = queueContentObject != null
                    ? queueContentObject.GetComponent<RectTransform>()
                    : null;
            }

            if (ticketQueueItemTemplate == null)
            {
                GameObject templateObject = FindSceneObject("QueueItemPlaceholder");
                if (templateObject != null)
                {
                    Image background = templateObject.GetComponent<Image>();
                    Button button = templateObject.GetComponent<Button>();
                    if (button == null)
                    {
                        button = templateObject.AddComponent<Button>();
                    }

                    button.targetGraphic = background;
                    Level1TicketQueueItemView template =
                        templateObject.GetComponent<Level1TicketQueueItemView>();
                    if (template == null)
                    {
                        template = templateObject.AddComponent<Level1TicketQueueItemView>();
                    }

                    template.BindReferences(
                        button,
                        background,
                        templateObject.GetComponentInChildren<TMP_Text>(true));
                    templateObject.SetActive(false);
                    ticketQueueItemTemplate = template;
                }
            }

            if (ticketContentObjects == null || ticketContentObjects.Length == 0)
            {
                ticketContentObjects = new[]
                {
                    FindSceneObject("TicketInfoPanel"),
                    FindSceneObject("ChatPanel"),
                    FindSceneObject("ProcessModule"),
                    FindSceneObject("TicketPanel"),
                    FindSceneObject("DataLockedPlaceholder"),
                    FindSceneObject("DataPanel"),
                    FindSceneObject("ActionBar")
                };
            }

            EnsureRuntimeSaveEvidenceButton();
            EnsureRuntimeChatEvidenceButton();
        }

        private void EnsureEvidenceDetailReferences()
        {
            if (evidenceDetailOverlay == null)
            {
                evidenceDetailOverlay = FindSceneObject("EvidenceDetailOverlay");
            }

            if (evidenceDetailOverlay == null)
            {
                return;
            }

            if (evidenceDetailTitleText == null)
            {
                evidenceDetailTitleText =
                    FindChildText(evidenceDetailOverlay.transform, "Txt_EvidenceDetailTitle");
            }

            if (evidenceDetailBodyText == null)
            {
                evidenceDetailBodyText =
                    FindChildText(evidenceDetailOverlay.transform, "Txt_EvidenceDetailBody");
            }

            if (evidenceDetailCloseButton == null)
            {
                evidenceDetailCloseButton =
                    FindChildButton(evidenceDetailOverlay.transform, "Btn_CloseEvidenceDetail");
            }
        }

        private void ConfigureEvidenceCardLayout(bool tutorialEvidenceLayout)
        {
            GameObject profileCard = profileText.transform.parent.gameObject;
            GameObject historyCard = historyText.transform.parent.gameObject;
            GameObject deviceCard = deviceLogText.transform.parent.gameObject;
            GameObject regionCard = regionStatusText.transform.parent.gameObject;

            profileCard.SetActive(true);
            historyCard.SetActive(true);
            deviceCard.SetActive(!tutorialEvidenceLayout);
            regionCard.SetActive(!tutorialEvidenceLayout);
            SetCardTitle(profileText, "资料01");
            SetCardTitle(historyText, "资料02");
            SetCardTitle(deviceLogText, "资料03");
            SetCardTitle(regionStatusText, "资料04");
        }

        private static void SetCardAnchors(
            GameObject cardObject,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            RectTransform rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetCardTitle(TMP_Text bodyText, string title)
        {
            if (bodyText == null || bodyText.transform.parent == null)
            {
                return;
            }

            Transform card = bodyText.transform.parent;
            TMP_Text titleText = null;
            Transform titleTransform = card.Find("Txt_Title");
            if (titleTransform != null)
            {
                titleText = titleTransform.GetComponent<TMP_Text>();
            }

            if (titleText == null)
            {
                TMP_Text[] texts = card.GetComponentsInChildren<TMP_Text>(true);
                foreach (TMP_Text text in texts)
                {
                    if (text != bodyText)
                    {
                        titleText = text;
                        break;
                    }
                }
            }

            if (titleText == null)
            {
                return;
            }

            titleText.gameObject.SetActive(true);
            titleText.text = title;
            titleText.enableAutoSizing = false;
            titleText.fontSize = 24f;
            titleText.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.overflowMode = TextOverflowModes.Truncate;

            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(18f, -52f);
            titleRect.offsetMax = new Vector2(-14f, -12f);

            ConfigureEvidenceCardTextLayout(bodyText, false);
        }

        private static void ConfigureEvidenceCardTextLayout(TMP_Text bodyText, bool actionVisible)
        {
            if (bodyText == null || bodyText.transform.parent == null)
            {
                return;
            }

            Transform card = bodyText.transform.parent;
            Transform titleTransform = card.Find("Txt_Title");
            if (titleTransform != null &&
                titleTransform.TryGetComponent(out RectTransform titleRect))
            {
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = Vector2.one;
                titleRect.offsetMin = new Vector2(18f, -52f);
                titleRect.offsetMax = new Vector2(-14f, -12f);
            }

            RectTransform bodyRect = bodyText.GetComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(18f, actionVisible ? 90f : 14f);
            bodyRect.offsetMax = new Vector2(-18f, -66f);
        }

        private TMP_Text GetEvidenceText(int evidenceIndex)
        {
            return evidenceIndex switch
            {
                0 => profileText,
                1 => historyText,
                2 => deviceLogText,
                3 => regionStatusText,
                _ => null
            };
        }

        /// <summary>
        /// 为旧场景在 AI 聊天气泡内创建证据流程按钮。
        /// </summary>
        private void EnsureRuntimeChatEvidenceButton()
        {
            if (chatEvidenceActionButton != null ||
                transferHumanButton == null ||
                aiReplyText == null)
            {
                return;
            }

            chatEvidenceActionButton = Instantiate(
                transferHumanButton,
                aiReplyText.transform.parent);
            chatEvidenceActionButton.gameObject.name = "Btn_ChatEvidenceAction";
            RectTransform rect = chatEvidenceActionButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.78f, 0.08f);
            rect.anchorMax = new Vector2(0.78f, 0.08f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(190f, 46f);
            rect.anchoredPosition = Vector2.zero;
            chatEvidenceActionButtonText =
                chatEvidenceActionButton.GetComponentInChildren<TMP_Text>(true);
            SetButtonLabel(chatEvidenceActionButtonText, "出示证据");
            chatEvidenceActionButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// 兼容只有三个操作按钮的旧场景。
        /// 复制“转人工”按钮作为“保留证据”，并把四个按钮重新均匀排列。
        /// </summary>
        private void EnsureRuntimeSaveEvidenceButton()
        {
            if (dataLookupButton != null)
            {
                SetButtonAnchor(primaryActionButton, 0.13f, 154f);
                SetButtonAnchor(dataLookupButton, 0.34f, 210f);
                SetButtonAnchor(transferHumanButton, 0.56f, 180f);
                SetButtonAnchor(markResolvedButton, 0.82f, 290f);
                if (saveEvidenceButton != null)
                {
                    saveEvidenceButton.gameObject.SetActive(false);
                }

                return;
            }

            if (saveEvidenceButton == null && transferHumanButton != null)
            {
                transferHumanButton.gameObject.name = "Btn_TransferHuman";
                saveEvidenceButton = Instantiate(
                    transferHumanButton,
                    transferHumanButton.transform.parent);
                saveEvidenceButton.gameObject.name = "Btn_SaveEvidence";
                TMP_Text label =
                    saveEvidenceButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = "保留证据";
                }
            }

            if (saveEvidenceButton == null)
            {
                return;
            }

            SetButtonAnchor(primaryActionButton, 0.13f, 250f);
            SetButtonAnchor(transferHumanButton, 0.38f, 250f);
            SetButtonAnchor(saveEvidenceButton, 0.63f, 250f);
            SetButtonAnchor(markResolvedButton, 0.87f, 250f);
        }

        private static void SetButtonAnchor(Button button, float horizontalAnchor, float width)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(horizontalAnchor, rect.anchorMin.y);
            rect.anchorMax = new Vector2(horizontalAnchor, rect.anchorMax.y);
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        }

        private static Button FindButton(string objectName)
        {
            GameObject buttonObject = FindSceneObject(objectName);
            return buttonObject != null
                ? buttonObject.GetComponent<Button>()
                : null;
        }

        private static Button FindChildButton(Transform parent, string buttonName)
        {
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == buttonName)
                {
                    return child.GetComponent<Button>();
                }
            }

            return null;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Transform sceneTransform in transforms)
            {
                if (sceneTransform.name == objectName)
                {
                    return sceneTransform.gameObject;
                }
            }

            return null;
        }

        private void NotifyTicketSelected(int ticketIndex) => TicketSelected?.Invoke(ticketIndex);

        private void NotifyViewDataRequested() => ViewDataRequested?.Invoke();
        private void NotifyFollowUpRequested() => FollowUpRequested?.Invoke();

        private void NotifyTransferHumanRequested() => TransferHumanRequested?.Invoke();
        private void NotifySaveEvidenceRequested() => SaveEvidenceRequested?.Invoke();
        private void NotifyChatEvidenceActionRequested() => ChatEvidenceActionRequested?.Invoke();
        private void NotifyEvidenceSelected(int evidenceIndex) => EvidenceSelected?.Invoke(evidenceIndex);
        private void NotifyEvidenceDetailKeywordClicked(string slotId) =>
            EvidenceDetailKeywordClicked?.Invoke(slotId);
        private void NotifyNotebookCancelRequested()
        {
            HideEvidenceNotebook();
            NotebookCancelRequested?.Invoke();
        }

        private void NotifyNotebookEvidence01Selected() => SelectNotebookEvidence(0);
        private void NotifyNotebookEvidence02Selected() => SelectNotebookEvidence(1);
        private void NotifyNotebookEvidence03Selected() => SelectNotebookEvidence(2);
        private void NotifyNotebookEvidence04Selected() => SelectNotebookEvidence(3);
        private void NotifyEvidence01Selected() => EvidenceSelected?.Invoke(0);
        private void NotifyEvidence02Selected() => EvidenceSelected?.Invoke(1);
        private void NotifyEvidence03Selected() => EvidenceSelected?.Invoke(2);
        private void NotifyEvidence04Selected() => EvidenceSelected?.Invoke(3);
        private void NotifyMarkResolvedRequested() => MarkResolvedRequested?.Invoke();
        private void NotifyResultActionRequested()
        {
            if (resolveTutorialHintVisible)
            {
                resolveTutorialHintVisible = false;
                resultPanel.SetActive(false);
                HighlightMarkResolvedButton();
                return;
            }

            ResultActionRequested?.Invoke();
        }

        private sealed class EvidenceDetailLinkHandler :
            MonoBehaviour,
            IPointerDownHandler,
            IPointerClickHandler
        {
            private TMP_Text targetText;
            private Action<string> onLinkClicked;
            private int lastHandledFrame = -1;

            public void Configure(TMP_Text text, Action<string> callback)
            {
                targetText = text;
                onLinkClicked = callback;
            }

            public void OnPointerDown(PointerEventData eventData) =>
                TryNotifyLinkClick(eventData);

            public void OnPointerClick(PointerEventData eventData) =>
                TryNotifyLinkClick(eventData);

            private void TryNotifyLinkClick(PointerEventData eventData)
            {
                if (targetText == null ||
                    onLinkClicked == null ||
                    lastHandledFrame == Time.frameCount)
                {
                    return;
                }

                targetText.ForceMeshUpdate(true, true);
                Camera eventCamera = eventData.pressEventCamera != null
                    ? eventData.pressEventCamera
                    : eventData.enterEventCamera;
                int linkIndex = TMP_TextUtilities.FindIntersectingLink(
                    targetText,
                    eventData.position,
                    eventCamera);
                if (linkIndex < 0 || linkIndex >= targetText.textInfo.linkCount)
                {
                    return;
                }

                TMP_LinkInfo linkInfo = targetText.textInfo.linkInfo[linkIndex];
                string linkId = linkInfo.GetLinkID();
                if (!string.IsNullOrWhiteSpace(linkId))
                {
                    lastHandledFrame = Time.frameCount;
                    onLinkClicked.Invoke(linkId);
                }
            }
        }
    }
}
