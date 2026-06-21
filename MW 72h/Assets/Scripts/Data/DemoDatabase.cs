using System.Collections.Generic;
using UnityEngine;

namespace MingBay.Data
{
    /// <summary>
    /// 工单 Demo 使用的数据入口。
    /// 工单按阶段和列表顺序显示在左侧队列中。
    /// </summary>
    [CreateAssetMenu(fileName = "DemoDatabase", menuName = "明湾/数据/Demo 数据库")]
    public sealed class DemoDatabase : ScriptableObject
    {
        [Header("工单顺序")]
        [SerializeField]
        [InspectorName("工单列表")]
        [Tooltip("按左侧队列显示顺序排列工单。Demo 至少需要配置一条。")]
        private List<TicketData> tickets = new();

        public int TicketCount => tickets.Count;
        public IReadOnlyList<TicketData> Tickets => tickets;

        /// <summary>
        /// 按索引读取工单；越界时返回空，避免流程脚本直接报错。
        /// </summary>
        public TicketData GetTicket(int index)
        {
            return index >= 0 && index < tickets.Count ? tickets[index] : null;
        }

        /// <summary>
        /// 按阶段读取工单，保持数据库中的原始排列顺序。
        /// </summary>
        public List<TicketData> GetTicketsByStage(string stageId)
        {
            List<TicketData> result = new();
            foreach (TicketData ticket in tickets)
            {
                if (ticket != null && ticket.StageId == stageId)
                {
                    result.Add(ticket);
                }
            }

            return result;
        }
    }
}
