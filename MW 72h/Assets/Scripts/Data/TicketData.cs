using UnityEngine;

namespace MingBay.Data
{
    /// <summary>
    /// 一条工单的静态配置数据。
    /// 策划可以直接在 Inspector 中调整文本和证据规则，不需要修改代码。
    /// </summary>
    [CreateAssetMenu(fileName = "Ticket_", menuName = "明湾/数据/工单配置")]
    public sealed class TicketData : ScriptableObject
    {
        [Header("基础信息")]
        [SerializeField]
        [InspectorName("工单 ID")]
        [Tooltip("工单的唯一编号，例如 T_001。禁止留空或与其他工单重复。")]
        private string ticketId = "T_001";

        [SerializeField]
        [InspectorName("阶段 ID")]
        [Tooltip("工单所属阶段。最小 Demo 暂时统一使用 Stage_Intro。")]
        private string stageId = "Stage_Intro";

        [SerializeField]
        [InspectorName("工单标题")]
        [Tooltip("显示在主界面顶部的工单名称，建议简短描述居民遇到的问题。")]
        private string title;

        [SerializeField]
        [InspectorName("用户姓名")]
        [Tooltip("当前求助居民的显示名称。")]
        private string userName;

        [SerializeField]
        [InspectorName("所属区域")]
        [Tooltip("用户所在区域，用于玩家判断不同工单之间是否存在关联。")]
        private string region;

        [SerializeField]
        [InspectorName("问题类型")]
        [Tooltip("工单分类，例如门禁、退款、医疗设备或交通。")]
        private string issueType;

        [SerializeField]
        [InspectorName("等待时间")]
        [Tooltip("界面展示用的等待时长文本，例如“等待 01:42:18”。")]
        private string waitTimeText;

        [Header("对话内容")]
        [SerializeField]
        [InspectorName("用户求助")]
        [Tooltip("用户向明湾通提交的原始求助内容。")]
        [TextArea(3, 6)]
        private string userMessage;

        [SerializeField]
        [InspectorName("AI 回复")]
        [Tooltip("系统自动给出的回复。应与资料中的事实形成可判断的关系。")]
        [TextArea(3, 6)]
        private string aiReply;

        [Header("资料内容")]
        [SerializeField]
        [InspectorName("用户资料")]
        [Tooltip("点击“查看资料”后显示的用户基本信息。")]
        [TextArea(2, 5)]
        private string profileText;

        [SerializeField]
        [InspectorName("历史工单")]
        [Tooltip("该用户或同区域的历史处理记录，用于暴露重复关闭等异常。")]
        [TextArea(2, 6)]
        private string historyText;

        [SerializeField]
        [InspectorName("设备日志")]
        [Tooltip("设备上报的状态和日志信息。没有内容时可以填写“暂无设备日志”。")]
        [TextArea(2, 6)]
        private string deviceLogText;

        [SerializeField]
        [InspectorName("区域状态")]
        [Tooltip("该区域当前的系统标签、维护状态或异常说明。")]
        [TextArea(2, 6)]
        private string regionStatusText;

        [Header("证据规则")]
        [SerializeField]
        [InspectorName("包含有效证据")]
        [Tooltip("勾选后，玩家选择“保留证据”会成功记录该工单的证据。")]
        private bool hasEvidence;

        [SerializeField]
        [InspectorName("证据 ID")]
        [Tooltip("有效证据的唯一编号，例如 E_DOOR_BATCH。没有证据时请留空。")]
        private string evidenceId;

        [Header("处理结果")]
        [SerializeField]
        [InspectorName("保留证据结果文本")]
        [Tooltip("玩家选择“保留证据”后显示的反馈文本。")]
        [TextArea(2, 5)]
        private string onSaveEvidenceText;

        [SerializeField]
        [InspectorName("标记已解决结果文本")]
        [Tooltip("玩家选择“标记已解决”后显示的反馈文本。")]
        [TextArea(2, 5)]
        private string onResolvedText;

        public string TicketId => ticketId;
        public string StageId => stageId;
        public string Title => title;
        public string UserName => userName;
        public string Region => region;
        public string IssueType => issueType;
        public string WaitTimeText => waitTimeText;
        public string UserMessage => userMessage;
        public string AiReply => aiReply;
        public string ProfileText => profileText;
        public string HistoryText => historyText;
        public string DeviceLogText => deviceLogText;
        public string RegionStatusText => regionStatusText;
        public bool HasEvidence => hasEvidence;
        public string EvidenceId => evidenceId;
        public string OnSaveEvidenceText => onSaveEvidenceText;
        public string OnResolvedText => onResolvedText;
    }
}
