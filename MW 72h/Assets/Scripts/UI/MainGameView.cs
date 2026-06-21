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
        [InspectorName("标记已解决按钮")]
        [Tooltip("玩家决定关闭工单时使用。查看资料前保持不可点击。")]
        private Button markResolvedButton;

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
        public event Action<int> EvidenceSelected;
        public event Action MarkResolvedRequested;
        public event Action ResultActionRequested;

        private readonly List<TicketQueueItemView> queueItems = new();
        private readonly Button[] evidenceButtons = new Button[4];
        private TMP_Text resultActionButtonText;
        private TMP_Text primaryActionButtonText;
        private TMP_Text transferHumanButtonText;
        private bool dataExpanded;

        private void Awake()
        {
            EnsureRuntimeReferences();
            CacheButtonLabels();
            EnsureEvidenceButtons();
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
            dataExpanded = false;
            SetButtonLabel(primaryActionButtonText, "查看资料");
            SetButtonLabel(transferHumanButtonText, "转人工");
            primaryActionButton.interactable = true;
            transferHumanButton.interactable = false;
            markResolvedButton.interactable = false;
            SetEvidenceButtonsInteractable(false);
        }

        /// <summary>
        /// 展开资料并允许玩家做最终选择。
        /// </summary>
        public void ShowData(TicketData ticket)
        {
            profileText.text = $"资料01｜用户资料\n{ticket.ProfileText}";
            historyText.text = $"资料02｜历史工单记录\n{ticket.HistoryText}";
            deviceLogText.text = $"资料03｜AI 处理建议\n{ticket.DeviceLogText}";
            regionStatusText.text = $"资料04｜设备 / 系统日志\n{ticket.RegionStatusText}";

            dataPanel.SetActive(true);
            dataExpanded = true;
            statusText.text = "状态：请选择追问、转人工或标记已解决";
            SetButtonLabel(primaryActionButtonText, "追问");
            primaryActionButton.interactable = true;
            transferHumanButton.interactable = true;
            markResolvedButton.interactable = true;
            SetEvidenceButtonsInteractable(false);
        }

        /// <summary>
        /// 教程工单没有证据选择时，引导玩家使用标记已解决。
        /// </summary>
        public void ShowResolutionHint(string actionText)
        {
            aiReplyText.text = $"{aiReplyText.text}\n\n{actionText}";
            statusText.text = "教程提示：请使用“标记已解决”完成工单";
            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
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
            aiReplyText.text =
                $"{ticket.AiReply}\n\n{actionText}\n\nA07：{evidencePrompt}";
            statusText.text = "状态：请点击资料01～资料04提交证据";
            primaryActionButton.interactable = false;
            transferHumanButton.interactable = false;
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
            markResolvedButton.interactable = false;
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
            resultActionButtonText =
                resultActionButton != null
                    ? resultActionButton.GetComponentInChildren<TMP_Text>(true)
                    : null;
        }

        private static void SetButtonLabel(TMP_Text label, string content)
        {
            if (label != null)
            {
                label.text = content;
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
        private void NotifyEvidence01Selected() => EvidenceSelected?.Invoke(0);
        private void NotifyEvidence02Selected() => EvidenceSelected?.Invoke(1);
        private void NotifyEvidence03Selected() => EvidenceSelected?.Invoke(2);
        private void NotifyEvidence04Selected() => EvidenceSelected?.Invoke(3);
        private void NotifyMarkResolvedRequested() => MarkResolvedRequested?.Invoke();
        private void NotifyResultActionRequested() => ResultActionRequested?.Invoke();
    }
}
