using System;
using System.Collections.Generic;
using UnityEngine;

namespace MingBay.Data
{
    /// <summary>
    /// Project-level runtime configuration entry.
    /// Import tools write spreadsheet-derived assets here, and gameplay systems read from it.
    /// </summary>
    [CreateAssetMenu(fileName = "MingBayProjectDatabase", menuName = "MingBay/Data/Project Database")]
    public sealed class MingBayProjectDatabase : ScriptableObject
    {
        [Header("Tickets")]
        [SerializeField]
        private List<TicketData> tickets = new();

        [Header("Stages")]
        [SerializeField]
        private string[] stageOrder = { "N1" };

        [SerializeField]
        private string[] stageDisplayNames = { "第一夜" };

        public int TicketCount => tickets != null ? tickets.Count : 0;
        public IReadOnlyList<TicketData> Tickets => tickets;
        public IReadOnlyList<string> StageOrder => stageOrder ?? Array.Empty<string>();
        public IReadOnlyList<string> StageDisplayNames => stageDisplayNames ?? Array.Empty<string>();
        public string[] StageOrderArray => stageOrder ?? Array.Empty<string>();
        public string[] StageDisplayNameArray => stageDisplayNames ?? Array.Empty<string>();

        public TicketData GetTicket(int index)
        {
            return tickets != null && index >= 0 && index < tickets.Count
                ? tickets[index]
                : null;
        }

        public List<TicketData> GetTicketsByStage(string stageId)
        {
            List<TicketData> result = new();
            if (tickets == null)
            {
                return result;
            }

            foreach (TicketData ticket in tickets)
            {
                if (ticket != null && ticket.StageId == stageId)
                {
                    result.Add(ticket);
                }
            }

            return result;
        }

        public string GetStageDisplayName(int index)
        {
            return stageDisplayNames != null &&
                   index >= 0 &&
                   index < stageDisplayNames.Length
                ? stageDisplayNames[index]
                : string.Empty;
        }
    }
}
