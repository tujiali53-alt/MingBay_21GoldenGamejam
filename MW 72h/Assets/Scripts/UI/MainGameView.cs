using System;
using System.Collections.Generic;
using MingBay.Core;
using MingBay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MingBay.UI
{
    /// <summary>
    /// 游戏主界面的显示与按钮入口。
    /// 该脚本不判断证据和流程，只负责把玩家操作通知给 GameFlowManager。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("明湾/UI/游戏主界面")]
    public sealed class MainGameView : MonoBehaviour
    {
        [Header("工单队列")]
        [SerializeField]
        [InspectorName("工单队列容器")]
        [Tooltip("运行时生成工单按钮的父节点。左侧工单会按数据库顺序排列。")]
        private RectTransform ticketQueueContent;

        [SerializeField]
        [InspectorName("工单队列模板")]
        [Tooltip("用于复制工单按钮的隐藏模板，必须绑定 TicketQueueItemView。")]
        private TicketQueueItemView ticketQueueItemTemplate;

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
        [Tooltip("显示用户、区域、类型和等待时间。")]
        private TMP_Text ticketMetaText;

        [SerializeField]
        [InspectorName("用户消息文本")]
        [Tooltip("显示居民提交的求助内容。")]
        private TMP_Text userMessageText;

        [SerializeField]
        [InspectorName("AI 回复文本")]
        [Tooltip("显示系统自动回复内容。")]
        private TMP_Text aiReplyText;

        [Header("资料面板")]
        [SerializeField]
        [InspectorName("资料面板对象")]
        [Tooltip("点击“查看资料”后显示的完整资料面板。")]
        private GameObject dataPanel;

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

        [Header("操作按钮")]
        [SerializeField]
        [FormerlySerializedAs("viewDataButton")]
        [InspectorName("主操作按钮")]
        [Tooltip("初始显示“查看资料”，资料展开后切换为“追问”。")]
        private Button primaryActionButton;

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
        [Tooltip("玩家决定关闭工单时使用。查看资料前保持不可点击。")]
        private Button markResolvedButton;

        [SerializeField]
        [InspectorName("聊天证据操作按钮")]
        [Tooltip("显示在 AI 聊天气泡中的“出示证据/完成对话”按钮。旧场景未绑定时会在运行时自动创建。")]
        private Button chatEvidenceActionButton;

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
        public event Action MarkResolvedRequested;
        public event Action ResultActionRequested;

        private readonly List<TicketQueueItemView> queueItems = new();
        private readonly Button[] evidenceButtons = new Button[4];
        private TMP_Text resultActionButtonText;
        private TMP_Text primaryActionButtonText;
        private TMP_Text transferHumanButtonText;
        private TMP_Text saveEvidenceButtonText;
        private TMP_Text chatEvidenceActionButtonText;
        private bool dataExpanded;
        private bool resolveTutorialHintVisible;
        private ColorBlock defaultMarkResolvedColors;

        private void Awake()
        {
            EnsureRuntimeReferences();
            CacheButtonLabels();
            EnsureEvidenceButtons();
            ConfigureConversationText();
            if (markResolvedButton != null)
            {
                defaultMarkResolvedColors = markResolvedButton.colors;
            }
        }

        private void OnEnable()
        {
            if (primaryActionButton != null)
            {
                primaryActionButton.onClick.AddListener(NotifyPrimaryActionRequested);
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

            if (markResolvedButton != null)
            {
                markResolvedButton.onClick.AddListener(NotifyMarkResolvedRequested);
            }

            if (resultActionButton != null)
            {
                resultActionButton.onClick.AddListener(NotifyResultActionRequested);
            }
        }

        private void OnDisable()
        {
            if (primaryActionButton != null)
            {
                primaryActionButton.onClick.RemoveListener(NotifyPrimaryActionRequested);
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

            if (markResolvedButton != null)
            {
                markResolvedButton.onClick.RemoveListener(NotifyMarkResolvedRequested);
            }

            if (resultActionButton != null)
            {
                resultActionButton.onClick.RemoveListener(NotifyResultActionRequested);
            }
        }

        /// <summary>
        /// 根据数据库生成左侧工单按钮。界面不读取配置文件，只消费传入的数据。
        /// </summary>
        public void BuildTicketQueue(IReadOnlyList<TicketData> tickets)
        {
            for (int childIndex = ticketQueueContent.childCount - 1; childIndex >= 0; childIndex--)
            {
                TicketQueueItemView existingItem =
                    ticketQueueContent.GetChild(childIndex).GetComponent<TicketQueueItemView>();
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

            for (int index = 0; index < tickets.Count; index++)
            {
                TicketQueueItemView item = Instantiate(ticketQueueItemTemplate, ticketQueueContent);
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

            SetTicketContentVisible(false);
            resultPanel.SetActive(false);
            resolveTutorialHintVisible = false;
            RestoreMarkResolvedHighlight();
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
            ticketTitleText.text = ticket.Title;
            ticketMetaText.text =
                $"用户：{ticket.UserName}\n区域：{ticket.Region}\n类型：{ticket.IssueType}\n{ticket.WaitTimeText}";
            userMessageText.text = ticket.UserMessage;
            aiReplyText.text = ticket.AiReply;

            SetTicketContentVisible(true);
            dataPanel.SetActive(false);
            resultPanel.SetActive(false);
            resolveTutorialHintVisible = false;
            RestoreMarkResolvedHighlight();
            dataExpanded = false;
            SetButtonLabel(primaryActionButtonText, "查看资料");
            SetButtonLabel(transferHumanButtonText, "转人工");
            SetButtonLabel(saveEvidenceButtonText, "保留证据");
            primaryActionButton.interactable = true;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
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

            if (tutorialEvidenceLayout)
            {
                profileText.text = $"01：{ticket.ProfileText}";
                historyText.text = $"02：{ticket.HistoryText}";
            }
            else
            {
                profileText.text = $"资料01｜用户资料\n{ticket.ProfileText}";
                historyText.text = $"资料02｜历史工单记录\n{ticket.HistoryText}";
                deviceLogText.text = $"资料03｜AI 处理建议\n{ticket.DeviceLogText}";
                regionStatusText.text = $"资料04｜设备 / 系统日志\n{ticket.RegionStatusText}";
            }

            dataPanel.SetActive(true);
            ConfigureEvidenceCardLayout(tutorialEvidenceLayout);
            if (stagedEvidenceFlow)
            {
                AddEvidenceRetentionHint(profileText);
                AddEvidenceRetentionHint(historyText);
                AddEvidenceRetentionHint(deviceLogText);
                AddEvidenceRetentionHint(regionStatusText);
            }

            dataExpanded = true;
            statusText.text = "状态：请选择追问、转人工、保留证据或标记已解决";
            SetButtonLabel(primaryActionButtonText, "追问");
            primaryActionButton.interactable = true;
            transferHumanButton.interactable = true;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = true;
            SetEvidenceButtonsInteractable(stagedEvidenceFlow);
        }

        /// <summary>
        /// 在资料查看阶段记录玩家选中的证据候选项，等待点击“保留证据”确认。
        /// </summary>
        public void ShowEvidenceCandidateSelected(int evidenceIndex)
        {
            statusText.text =
                $"状态：已选择资料{evidenceIndex + 1:00}，请点击“保留证据”确认";
            saveEvidenceButton.interactable = true;
        }

        /// <summary>
        /// 教程工单没有证据选择时，引导玩家使用标记已解决。
        /// </summary>
        public void ShowResolutionHint(string actionText)
        {
            if (!string.IsNullOrWhiteSpace(actionText))
            {
                aiReplyText.text = $"{aiReplyText.text}\n\n{actionText}";
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
            aiReplyText.text =
                $"{ticket.AiReply}{actionSection}\n\nA07：{evidencePrompt}";
            statusText.text = "状态：请点击资料01～资料04提交证据";
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
        public void ShowResidentFollowUp(string followUpText, bool hasRemainingFollowUp)
        {
            if (string.IsNullOrWhiteSpace(followUpText))
            {
                return;
            }

            userMessageText.text = $"{userMessageText.text}\n\n{followUpText}";
            RefreshResidentMessageLayout();
            statusText.text = hasRemainingFollowUp
                ? "状态：居民仍有补充说明，可继续点击“追问”"
                : "状态：居民补充说明已全部显示";
            primaryActionButton.interactable = hasRemainingFollowUp;
        }

        /// <summary>
        /// 第二张教程工单追问结束后，说明无法完美满足所有诉求，并引导关闭工单。
        /// </summary>
        public void ShowResolveTutorialHint()
        {
            resolveTutorialHintVisible = true;
            resultTitleText.text = "教程提示";
            resultDescriptionText.text =
                "不是每次都能完美解决居民诉求。游戏居民可能态度不好，" +
                "也可能要求太高、很难解决。你可以按下这个按钮轻松解决。";
            resultMetricsText.text = "关闭提示后，“标记已解决”按钮会被高亮。";
            SetButtonLabel(resultActionButtonText, "我知道了");
            resultPanel.SetActive(true);
        }

        private void HighlightMarkResolvedButton()
        {
            ColorBlock colors = markResolvedButton.colors;
            colors.normalColor = new Color(0.95f, 0.68f, 0.18f, 1f);
            colors.highlightedColor = new Color(1f, 0.78f, 0.28f, 1f);
            colors.selectedColor = colors.highlightedColor;
            markResolvedButton.colors = colors;
            markResolvedButton.interactable = true;
            statusText.text = "教程提示：请点击高亮的“标记已解决”";
        }

        private void RestoreMarkResolvedHighlight()
        {
            if (markResolvedButton != null)
            {
                markResolvedButton.colors = defaultMarkResolvedColors;
            }
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
                $"已保留资料{evidenceIndex + 1:00}，现在可点击“转人工”";
            saveEvidenceButton.interactable = false;
            SetEvidenceButtonsInteractable(true);
        }

        /// <summary>
        /// 尚未保留证据时阻止进入人工核验阶段。
        /// </summary>
        public void ShowRetainedEvidenceRequired()
        {
            statusText.text = "请先点击一份资料并选择“保留证据”，再转人工";
        }

        /// <summary>
        /// 转人工后在聊天区域等待玩家主动点击“出示证据”。
        /// </summary>
        public void ShowEvidencePresentationRequest(
            TicketData ticket,
            string actionText,
            string evidencePrompt)
        {
            aiReplyText.text =
                $"{ticket.AiReply}\n\n{actionText}\n\nA07：{evidencePrompt}";
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
            aiReplyText.text =
                $"{aiReplyText.text}\n\n玩家：出示资料{evidenceIndex + 1:00}\n\n" +
                "A07：已收到您出示的证据，正在核验相关记录。";
            userMessageText.text =
                $"{userMessageText.text}\n\n{safeUserReply}";
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
            aiReplyText.text = $"{aiReplyText.text}\n\n{feedbackText}";
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
            aiReplyText.text = $"{aiReplyText.text}\n\n{feedbackText}";
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
            resultDescriptionText.text = resultDescription;
            resultMetricsText.text =
                $"已解决：{metrics.ResolvedCount}\n" +
                $"人工转接：{metrics.TransferCount}\n" +
                $"用户满意度：{metrics.UserSatisfaction:+#;-#;0}\n" +
                $"证据数量：{metrics.EvidenceCount}\n" +
                $"A-07 风险：{metrics.A07Risk:+#;-#;0}\n" +
                $"本次证据：{(evidenceSaved ? "已保存" : "未保存")}";
            if (resultActionButtonText != null)
            {
                resultActionButtonText.text = resultActionLabel;
            }

            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
            chatEvidenceActionButton.gameObject.SetActive(false);
            SetEvidenceButtonsInteractable(false);
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

        private static void SetButtonLabel(TMP_Text label, string content)
        {
            if (label != null)
            {
                label.text = content;
            }
        }

        private static void AddEvidenceClickHint(TMP_Text evidenceText)
        {
            const string ClickHint = "【点击此资料提交证据】";
            if (evidenceText != null && !evidenceText.text.Contains(ClickHint))
            {
                evidenceText.text = $"{evidenceText.text}\n\n{ClickHint}";
            }
        }

        private static void AddEvidenceRetentionHint(TMP_Text evidenceText)
        {
            const string RetentionHint = "【点击此资料作为待保留证据】";
            if (evidenceText != null && !evidenceText.text.Contains(RetentionHint))
            {
                evidenceText.text = $"{evidenceText.text}\n\n{RetentionHint}";
            }
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

                evidenceText.enableAutoSizing = true;
                evidenceText.fontSizeMin = 8f;
                evidenceText.fontSizeMax = 14f;

                GameObject cardObject = evidenceText.transform.parent.gameObject;
                Button button = cardObject.GetComponent<Button>();
                if (button == null)
                {
                    button = cardObject.AddComponent<Button>();
                }

                button.targetGraphic = cardObject.GetComponent<Image>();
                button.interactable = false;
                evidenceButtons[index] = button;
            }

            if (evidenceButtons[0] != null)
            {
                evidenceButtons[0].onClick.AddListener(NotifyEvidence01Selected);
            }

            if (evidenceButtons[1] != null)
            {
                evidenceButtons[1].onClick.AddListener(NotifyEvidence02Selected);
            }

            if (evidenceButtons[2] != null)
            {
                evidenceButtons[2].onClick.AddListener(NotifyEvidence03Selected);
            }

            if (evidenceButtons[3] != null)
            {
                evidenceButtons[3].onClick.AddListener(NotifyEvidence04Selected);
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

        private void SetTicketContentVisible(bool isVisible)
        {
            foreach (GameObject contentObject in ticketContentObjects)
            {
                if (contentObject != null)
                {
                    contentObject.SetActive(isVisible);
                }
            }
        }

        /// <summary>
        /// 兼容尚未重新生成的旧 GameScene。
        /// 通过对象名称复用旧的队列占位项，不要求策划手动重新绑定 Inspector。
        /// </summary>
        private void EnsureRuntimeReferences()
        {
            if (ticketQueueContent == null)
            {
                GameObject queuePanel = FindSceneObject("TicketQueuePanel");
                ticketQueueContent = queuePanel != null
                    ? queuePanel.GetComponent<RectTransform>()
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
                    TicketQueueItemView template =
                        templateObject.GetComponent<TicketQueueItemView>();
                    if (template == null)
                    {
                        template = templateObject.AddComponent<TicketQueueItemView>();
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
                    FindSceneObject("TicketPanel"),
                    FindSceneObject("DataLockedPlaceholder"),
                    FindSceneObject("DataPanel"),
                    FindSceneObject("ActionBar")
                };
            }

            EnsureRuntimeSaveEvidenceButton();
            EnsureRuntimeChatEvidenceButton();
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
            SetCardTitle(profileText, tutorialEvidenceLayout ? "具体证据 01" : "资料01 用户资料");
            SetCardTitle(historyText, tutorialEvidenceLayout ? "具体证据 02" : "资料02 历史工单");

            SetCardAnchors(
                profileCard,
                tutorialEvidenceLayout ? new Vector2(0.04f, 0.52f) : new Vector2(0.04f, 0.69f),
                tutorialEvidenceLayout ? new Vector2(0.96f, 0.86f) : new Vector2(0.96f, 0.88f));
            SetCardAnchors(
                historyCard,
                tutorialEvidenceLayout ? new Vector2(0.04f, 0.12f) : new Vector2(0.04f, 0.47f),
                tutorialEvidenceLayout ? new Vector2(0.96f, 0.46f) : new Vector2(0.96f, 0.66f));
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
            TMP_Text[] texts = bodyText.transform.parent.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text != bodyText)
                {
                    text.text = title;
                    return;
                }
            }
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

            SetButtonAnchor(primaryActionButton, 0.13f);
            SetButtonAnchor(transferHumanButton, 0.38f);
            SetButtonAnchor(saveEvidenceButton, 0.63f);
            SetButtonAnchor(markResolvedButton, 0.87f);
        }

        private static void SetButtonAnchor(Button button, float horizontalAnchor)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(horizontalAnchor, rect.anchorMin.y);
            rect.anchorMax = new Vector2(horizontalAnchor, rect.anchorMax.y);
            rect.sizeDelta = new Vector2(250f, rect.sizeDelta.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
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

        private void NotifyPrimaryActionRequested()
        {
            if (dataExpanded)
            {
                FollowUpRequested?.Invoke();
                return;
            }

            ViewDataRequested?.Invoke();
        }

        private void NotifyTransferHumanRequested() => TransferHumanRequested?.Invoke();
        private void NotifySaveEvidenceRequested() => SaveEvidenceRequested?.Invoke();
        private void NotifyChatEvidenceActionRequested() => ChatEvidenceActionRequested?.Invoke();
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
    }
}
