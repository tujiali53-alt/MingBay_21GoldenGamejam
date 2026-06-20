using System.Collections.Generic;
using UnityEngine;

namespace MingBay.Data
{
    /// <summary>
    /// 最小 Demo 使用的数据入口。
    /// 当前仅配置一条工单，后续可直接扩展为四条工单列表。
    /// </summary>
    [CreateAssetMenu(fileName = "DemoDatabase", menuName = "明湾/数据/Demo 数据库")]
    public sealed class DemoDatabase : ScriptableObject
    {
        [Header("工单顺序")]
        [SerializeField]
        [InspectorName("工单列表")]
        [Tooltip("按玩家实际处理顺序排列工单。最小纵向切片至少需要配置一条。")]
        private List<TicketData> tickets = new();

        public int TicketCount => tickets.Count;

        /// <summary>
        /// 按索引读取工单；越界时返回空，避免流程脚本直接报错。
        /// </summary>
        public TicketData GetTicket(int index)
        {
            return index >= 0 && index < tickets.Count ? tickets[index] : null;
        }
    }
}
