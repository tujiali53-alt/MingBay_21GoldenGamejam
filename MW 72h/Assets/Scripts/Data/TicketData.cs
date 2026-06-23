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
        [Tooltip("工单的唯一编号，例如 T_D01_001。禁止留空或与其他工单重复。")]
        private string ticketId = "T_D01_001";

        [SerializeField]
        [InspectorName("阶段 ID")]
        [Tooltip("工单所属阶段，例如 Stage_Tutorial 或 Stage_Day1。")]
        private string stageId = "Stage_Tutorial";

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
        [InspectorName("资料03：AI 处理建议")]
        [Tooltip("AI 根据当前信息生成的处理建议和内部标签。")]
        [TextArea(2, 6)]
        private string deviceLogText;

        [SerializeField]
        [InspectorName("资料04：设备或系统日志")]
        [Tooltip("设备日志、平台记录或其他可核验的系统数据。")]
        [TextArea(2, 6)]
        private string regionStatusText;

        [Header("处置流程")]
        [SerializeField]
        [InspectorName("追问内容")]
        [Tooltip("玩家点击“追问”后追加显示的用户说明。")]
        [TextArea(2, 5)]
        private string followUpText;

        [SerializeField]
        [InspectorName("连续追问台词")]
        [Tooltip("每点击一次“追问”依次显示一条。填写后优先使用该列表；留空时使用上方单条追问内容。")]
        private string[] followUpLines;

        [SerializeField]
        [InspectorName("转人工反馈")]
        [Tooltip("玩家点击“转人工”后显示的系统与 A-07 反馈。")]
        [TextArea(2, 5)]
        private string transferText =
            "AI《明湾通》：已转人工部门继续处理。\nA07：您好，《明湾通》A07 客服在线为您服务。";

        [SerializeField]
        [InspectorName("需要选择证据")]
        [Tooltip("勾选后，追问或转人工之后必须从四份资料中选择一份证据。教程工单可关闭。")]
        private bool requiresEvidenceSelection = true;

        [SerializeField]
        [InspectorName("允许直接保留证据")]
        [Tooltip("勾选后，玩家需要先在资料面板选择并保留证据，再转人工并在聊天中主动出示。适用于教程第一单和正式证据工单。")]
        private bool allowDirectEvidenceSave;

        [SerializeField]
        [InspectorName("提交证据后完成工单")]
        [Tooltip("勾选后，提交正确或错误证据会直接结算工单；关闭后会返回工单处理界面，等待玩家标记已解决。")]
        private bool finishOnEvidenceSubmission = true;

        [SerializeField]
        [InspectorName("证据请求文本")]
        [Tooltip("进入证据选择阶段时显示的提示，例如“请出示证据”。")]
        private string evidencePromptText = "请从四份资料中选择能够支持用户诉求的证据。";

        [Header("证据规则")]
        [SerializeField]
        [InspectorName("包含有效证据")]
        [Tooltip("勾选后，玩家提交正确资料时会成功记录该工单的证据。")]
        private bool hasEvidence;

        [SerializeField]
        [InspectorName("证据 ID")]
        [Tooltip("有效证据的唯一编号，例如 E_DOOR_BATCH。没有证据时请留空。")]
        private string evidenceId;

        [SerializeField]
        [Range(0, 3)]
        [InspectorName("正确证据序号")]
        [Tooltip("正确资料在四份资料中的序号：0=资料01，1=资料02，2=资料03，3=资料04。")]
        private int correctEvidenceIndex;

        [Header("处理结果")]
        [SerializeField]
        [InspectorName("正确证据结果文本")]
        [Tooltip("玩家提交正确资料作为证据后显示的反馈文本。")]
        [TextArea(2, 5)]
        private string onSaveEvidenceText;

        [SerializeField]
        [InspectorName("错误证据结果文本")]
        [Tooltip("玩家提交错误资料作为证据后显示的反馈文本。")]
        [TextArea(2, 5)]
        private string onWrongEvidenceText =
            "A07：后台未查询到能够支持该证据的相关记录，本次证据提交无效。";

        [SerializeField]
        [InspectorName("正确证据用户回应")]
        [Tooltip("人工客服核验正确证据后，用户在聊天中的回应。该回应显示完毕后才允许进入结算。")]
        [TextArea(2, 5)]
        private string correctEvidenceUserReply;

        [SerializeField]
        [InspectorName("错误证据用户回应")]
        [Tooltip("人工客服核验错误证据后，用户在聊天中的回应。该回应显示完毕后才允许进入结算。")]
        [TextArea(2, 5)]
        private string wrongEvidenceUserReply;

        [SerializeField]
        [InspectorName("标记已解决结果文本")]
        [Tooltip("玩家选择“标记已解决”后显示的反馈文本。")]
        [TextArea(2, 5)]
        private string onResolvedText;

        [Header("指标变化")]
        [SerializeField]
        [InspectorName("追问指标变化")]
        [Tooltip("点击“追问”后立即产生的指标变化。一般保持为 0。")]
        private MetricDelta followUpMetricDelta;

        [SerializeField]
        [InspectorName("转人工指标变化")]
        [Tooltip("点击“转人工”后立即产生的指标变化。人工转接通常填写 1。")]
        private MetricDelta transferMetricDelta;

        [SerializeField]
        [InspectorName("正确证据指标变化")]
        [Tooltip("提交正确证据时产生的指标变化。教程可保持已解决变化为 0，再由玩家标记已解决。")]
        private MetricDelta correctEvidenceMetricDelta;

        [SerializeField]
        [InspectorName("错误证据指标变化")]
        [Tooltip("提交错误证据时产生的指标变化。允许重试的教程工单一般保持为 0。")]
        private MetricDelta wrongEvidenceMetricDelta;

        [SerializeField]
        [InspectorName("标记已解决指标变化")]
        [Tooltip("直接标记已解决并完成工单时产生的指标变化。")]
        private MetricDelta resolvedMetricDelta;

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
        public string FollowUpText => followUpText;
        public string[] FollowUpLines =>
            followUpLines != null && followUpLines.Length > 0
                ? followUpLines
                : new[] { followUpText };
        public string TransferText => transferText;
        public bool RequiresEvidenceSelection => requiresEvidenceSelection;
        public bool AllowDirectEvidenceSave => allowDirectEvidenceSave;
        public bool FinishOnEvidenceSubmission => finishOnEvidenceSubmission;
        public string EvidencePromptText => evidencePromptText;
        public bool HasEvidence => hasEvidence;
        public string EvidenceId => evidenceId;
        public int CorrectEvidenceIndex => correctEvidenceIndex;
        public string OnSaveEvidenceText => onSaveEvidenceText;
        public string OnWrongEvidenceText => onWrongEvidenceText;
        public string CorrectEvidenceUserReply => correctEvidenceUserReply;
        public string WrongEvidenceUserReply => wrongEvidenceUserReply;
        public string OnResolvedText => onResolvedText;
        public MetricDelta FollowUpMetricDelta => followUpMetricDelta;
        public MetricDelta TransferMetricDelta => transferMetricDelta;
        public MetricDelta CorrectEvidenceMetricDelta => correctEvidenceMetricDelta;
        public MetricDelta WrongEvidenceMetricDelta => wrongEvidenceMetricDelta;
        public MetricDelta ResolvedMetricDelta => resolvedMetricDelta;
    }
}
