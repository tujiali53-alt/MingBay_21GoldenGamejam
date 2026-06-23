namespace MingBay.Core
{
    /// <summary>
    /// 工单处理流程的有限状态。
    /// 明确状态可以避免玩家跳过“查看资料”直接处理工单。
    /// </summary>
    public enum GameState
    {
        None,
        TicketSelection,
        ReadingTicket,
        ReviewingData,
        AwaitingEvidence,
        AwaitingEvidencePresentation,
        AwaitingDialogueCompletion,
        ShowingResult
    }
}
