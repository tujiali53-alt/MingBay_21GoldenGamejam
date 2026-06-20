namespace MingBay.Core
{
    /// <summary>
    /// 最小工单流程的有限状态。
    /// 明确状态可以避免玩家跳过“查看资料”直接处理工单。
    /// </summary>
    public enum GameState
    {
        None,
        ReadingTicket,
        ReviewingData,
        ShowingResult,
        Finished
    }
}
