using System;
using MingBay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MingBay.UI
{
    /// <summary>
    /// 左侧工单队列中的单个可点击项目。
    /// 仅负责显示工单编号、类型和处理状态，不负责推进游戏流程。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("明湾/UI/工单队列项目")]
    public sealed class TicketQueueItemView : MonoBehaviour
    {
        [Header("交互组件")]
        [SerializeField]
        [InspectorName("工单按钮")]
        [Tooltip("玩家点击该按钮后打开对应工单。已处理工单会自动禁用。")]
        private Button button;

        [SerializeField]
        [InspectorName("背景图片")]
        [Tooltip("用于显示普通、选中和已处理状态的灰色背景。")]
        private Image backgroundImage;

        [Header("显示文本")]
        [SerializeField]
        [InspectorName("工单摘要文本")]
        [Tooltip("分两行显示工单编号、类型和当前处理状态。")]
        private TMP_Text summaryText;

        [Header("状态颜色")]
        [SerializeField]
        [InspectorName("普通背景")]
        [Tooltip("尚未选择的待处理工单背景色。")]
        private Color normalColor = new(0.84f, 0.84f, 0.84f, 1f);

        [SerializeField]
        [InspectorName("选中背景")]
        [Tooltip("当前正在查看的工单背景色。")]
        private Color selectedColor = new(0.72f, 0.88f, 0.8f, 1f);

        [SerializeField]
        [InspectorName("已处理背景")]
        [Tooltip("已经完成处置的工单背景色。")]
        private Color processedColor = new(0.42f, 0.42f, 0.42f, 1f);

        private int ticketIndex;
        private string ticketId;
        private string issueType;
        private string userName;
        private Action<int> clickHandler;

        /// <summary>
        /// 为旧场景中的静态占位项目补齐运行时引用。
        /// 新生成的场景仍通过 Inspector 正常绑定。
        /// </summary>
        public void BindReferences(Button itemButton, Image itemBackground, TMP_Text itemSummary)
        {
            button = itemButton;
            backgroundImage = itemBackground;
            summaryText = itemSummary;
        }

        /// <summary>
        /// 绑定一条工单并注册点击回调。
        /// </summary>
        public void Configure(int index, TicketData ticket, Action<int> onClicked)
        {
            ticketIndex = index;
            ticketId = ticket.TicketId;
            issueType = ticket.IssueType;
            userName = ticket.UserName;
            clickHandler = onClicked;

            button.onClick.RemoveListener(NotifyClicked);
            button.onClick.AddListener(NotifyClicked);
            SetState(false, false);
        }

        /// <summary>
        /// 刷新当前队列项目的选中与处理状态。
        /// </summary>
        public void SetState(bool isSelected, bool isProcessed)
        {
            button.interactable = !isProcessed;
            summaryText.enableAutoSizing = false;
            summaryText.fontSize = 16f;

            if (isProcessed)
            {
                backgroundImage.color = processedColor;
                summaryText.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                summaryText.text = $"#{ticketId}\n{issueType}\n{userName}·6-24    已解决";
                return;
            }

            backgroundImage.color = isSelected ? selectedColor : normalColor;
            summaryText.color = new Color(0.16f, 0.16f, 0.16f, 1f);
            summaryText.text = isSelected
                ? $"#{ticketId}\n{issueType}\n{userName}·6-24    选中"
                : $"#{ticketId}\n{issueType}\n{userName}·6-24";
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(NotifyClicked);
            }
        }

        private void NotifyClicked()
        {
            clickHandler?.Invoke(ticketIndex);
        }
    }
}
