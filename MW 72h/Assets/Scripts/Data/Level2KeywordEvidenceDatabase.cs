using System;
using System.Collections.Generic;
using UnityEngine;

namespace MingBay.Data
{
    [Serializable]
    public sealed class Level2KeywordSlot
    {
        [SerializeField]
        private string slotId;

        [SerializeField]
        private string slotLabel;

        [SerializeField]
        private string answerSlotId;

        [SerializeField]
        private string keyword;

        [SerializeField]
        private int sourcePanelIndex;

        [SerializeField]
        private string sourcePanelTitle;

        [SerializeField]
        [TextArea(1, 3)]
        private string note;

        public string SlotId => slotId;
        public string SlotLabel => slotLabel;
        public string AnswerSlotId =>
            string.IsNullOrWhiteSpace(answerSlotId) ? slotId : answerSlotId;
        public string Keyword => keyword;
        public int SourcePanelIndex => sourcePanelIndex;
        public string SourcePanelTitle => sourcePanelTitle;
        public string Note => note;

        public string SummaryLine =>
            string.IsNullOrWhiteSpace(keyword)
                ? slotLabel
                : $"{slotLabel}: {keyword}";

        public Level2KeywordSlot(
            string slotId,
            string slotLabel,
            string keyword,
            int sourcePanelIndex,
            string sourcePanelTitle,
            string note,
            string answerSlotId = null)
        {
            this.slotId = slotId;
            this.slotLabel = slotLabel;
            this.answerSlotId = answerSlotId;
            this.keyword = keyword;
            this.sourcePanelIndex = sourcePanelIndex;
            this.sourcePanelTitle = sourcePanelTitle;
            this.note = note;
        }
    }

    [Serializable]
    public sealed class Level2EvidenceChain
    {
        [SerializeField]
        private string chainId;

        [SerializeField]
        private string ticketId;

        [SerializeField]
        [TextArea(3, 8)]
        private string aiSuggestionText;

        [SerializeField]
        [TextArea(2, 5)]
        private string correctResultText;

        [SerializeField]
        [TextArea(2, 5)]
        private string wrongResultText;

        [SerializeField]
        private Level2KeywordSlot[] keywordSlots = Array.Empty<Level2KeywordSlot>();

        public string ChainId => chainId;
        public string TicketId => ticketId;
        public string AiSuggestionText => aiSuggestionText;
        public string CorrectResultText => correctResultText;
        public string WrongResultText => wrongResultText;
        public IReadOnlyList<Level2KeywordSlot> KeywordSlots =>
            keywordSlots ?? Array.Empty<Level2KeywordSlot>();

        public Level2EvidenceChain(
            string chainId,
            string ticketId,
            string aiSuggestionText,
            string correctResultText,
            string wrongResultText,
            Level2KeywordSlot[] keywordSlots)
        {
            this.chainId = chainId;
            this.ticketId = ticketId;
            this.aiSuggestionText = aiSuggestionText;
            this.correctResultText = correctResultText;
            this.wrongResultText = wrongResultText;
            this.keywordSlots = keywordSlots ?? Array.Empty<Level2KeywordSlot>();
        }

        public bool ContainsSlot(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId) || keywordSlots == null)
            {
                return false;
            }

            foreach (Level2KeywordSlot slot in keywordSlots)
            {
                if (slot != null &&
                    string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsComplete(IReadOnlyCollection<string> collectedSlotIds)
        {
            if (keywordSlots == null || keywordSlots.Length == 0)
            {
                return false;
            }

            if (collectedSlotIds == null || collectedSlotIds.Count == 0)
            {
                return false;
            }

            foreach (Level2KeywordSlot slot in keywordSlots)
            {
                if (slot == null ||
                    string.IsNullOrWhiteSpace(slot.SlotId) ||
                    !HasCollectedSlot(collectedSlotIds, slot.SlotId))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasCollectedSlot(
            IReadOnlyCollection<string> collectedSlotIds,
            string slotId)
        {
            foreach (string collectedSlotId in collectedSlotIds)
            {
                if (string.Equals(collectedSlotId, slotId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [CreateAssetMenu(
        fileName = "Level2KeywordEvidenceDatabase",
        menuName = "MingBay/Data/Level2 Keyword Evidence Database")]
    public sealed class Level2KeywordEvidenceDatabase : ScriptableObject
    {
        [SerializeField]
        private List<Level2EvidenceChain> chains = new();

        public IReadOnlyList<Level2EvidenceChain> Chains => chains;

        public Level2EvidenceChain GetChainForTicket(string ticketId)
        {
            if (chains == null || string.IsNullOrWhiteSpace(ticketId))
            {
                return null;
            }

            foreach (Level2EvidenceChain chain in chains)
            {
                if (chain != null &&
                    string.Equals(chain.TicketId, ticketId, StringComparison.Ordinal))
                {
                    return chain;
                }
            }

            return null;
        }

        public void ReplaceChains(IEnumerable<Level2EvidenceChain> newChains)
        {
            chains = newChains != null
                ? new List<Level2EvidenceChain>(newChains)
                : new List<Level2EvidenceChain>();
        }
    }
}
