using System;
using MingBay.Data;
using TMPro;
using UnityEngine;
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
        [InspectorName("查看资料按钮")]
        [Tooltip("玩家点击后打开资料面板，并解锁最终处置按钮。")]
        private Button viewDataButton;

        [SerializeField]
        [InspectorName("保留证据按钮")]
        [Tooltip("玩家确认资料异常时使用。查看资料前保持不可点击。")]
        private Button saveEvidenceButton;

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
        [Tooltip("显示“证据已保留”或“工单已关闭”等结果标题。")]
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
        [InspectorName("返回主菜单按钮")]
        [Tooltip("单工单纵向切片结束后返回 TitleScene。")]
        private Button returnToTitleButton;

        public event Action ViewDataRequested;
        public event Action SaveEvidenceRequested;
        public event Action MarkResolvedRequested;
        public event Action ReturnToTitleRequested;

        private void OnEnable()
        {
            viewDataButton.onClick.AddListener(NotifyViewDataRequested);
            saveEvidenceButton.onClick.AddListener(NotifySaveEvidenceRequested);
            markResolvedButton.onClick.AddListener(NotifyMarkResolvedRequested);
            returnToTitleButton.onClick.AddListener(NotifyReturnToTitleRequested);
        }

        private void OnDisable()
        {
            if (viewDataButton != null)
            {
                viewDataButton.onClick.RemoveListener(NotifyViewDataRequested);
            }

            if (saveEvidenceButton != null)
            {
                saveEvidenceButton.onClick.RemoveListener(NotifySaveEvidenceRequested);
            }

            if (markResolvedButton != null)
            {
                markResolvedButton.onClick.RemoveListener(NotifyMarkResolvedRequested);
            }

            if (returnToTitleButton != null)
            {
                returnToTitleButton.onClick.RemoveListener(NotifyReturnToTitleRequested);
            }
        }

        /// <summary>
        /// 显示一条新工单，并恢复到“先查看资料”的初始状态。
        /// </summary>
        public void ShowTicket(
            TicketData ticket,
            int currentIndex,
            int totalCount,
            int evidenceCount,
            int resolvedCount)
        {
            ticketProgressText.text = $"工单 {currentIndex} / {totalCount}  ·  {ticket.TicketId}";
            statusText.text = "状态：等待核验";
            evidenceCountText.text = $"证据  {evidenceCount}";
            resolvedCountText.text = $"已解决  {resolvedCount}";
            ticketTitleText.text = ticket.Title;
            ticketMetaText.text =
                $"用户：{ticket.UserName}\n区域：{ticket.Region}\n类型：{ticket.IssueType}\n{ticket.WaitTimeText}";
            userMessageText.text = ticket.UserMessage;
            aiReplyText.text = ticket.AiReply;

            dataPanel.SetActive(false);
            resultPanel.SetActive(false);
            viewDataButton.interactable = true;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
        }

        /// <summary>
        /// 展开资料并允许玩家做最终选择。
        /// </summary>
        public void ShowData(TicketData ticket)
        {
            profileText.text = ticket.ProfileText;
            historyText.text = ticket.HistoryText;
            deviceLogText.text = ticket.DeviceLogText;
            regionStatusText.text = ticket.RegionStatusText;

            dataPanel.SetActive(true);
            statusText.text = "状态：资料已展开，请做出处置";
            viewDataButton.interactable = false;
            saveEvidenceButton.interactable = true;
            markResolvedButton.interactable = true;
        }

        /// <summary>
        /// 锁定操作并显示玩家选择造成的结果。
        /// </summary>
        public void ShowResult(
            string resultTitle,
            string resultDescription,
            bool evidenceSaved,
            int evidenceCount,
            int resolvedCount)
        {
            statusText.text = "状态：处理完成";
            evidenceCountText.text = $"证据  {evidenceCount}";
            resolvedCountText.text = $"已解决  {resolvedCount}";
            resultTitleText.text = resultTitle;
            resultDescriptionText.text = resultDescription;
            resultMetricsText.text =
                $"证据数量：{evidenceCount}\n已解决数量：{resolvedCount}\n本次证据记录：{(evidenceSaved ? "成功" : "无")}";

            viewDataButton.interactable = false;
            saveEvidenceButton.interactable = false;
            markResolvedButton.interactable = false;
            resultPanel.SetActive(true);
        }

        private void NotifyViewDataRequested() => ViewDataRequested?.Invoke();
        private void NotifySaveEvidenceRequested() => SaveEvidenceRequested?.Invoke();
        private void NotifyMarkResolvedRequested() => MarkResolvedRequested?.Invoke();
        private void NotifyReturnToTitleRequested() => ReturnToTitleRequested?.Invoke();
    }
}
