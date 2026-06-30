using System;
using System.Collections.Generic;
using UnityEngine;

namespace MingBay.Data
{
    [Serializable]
    public sealed class FinalEvidenceOption
    {
        [SerializeField]
        private string chainId;

        [SerializeField]
        [TextArea(2, 5)]
        private string displayTextCn;

        public string ChainId => chainId;
        public string DisplayTextCn => displayTextCn;

        public FinalEvidenceOption(string chainId, string displayTextCn)
        {
            this.chainId = chainId;
            this.displayTextCn = displayTextCn;
        }
    }

    [Serializable]
    public sealed class FinalConfrontationQuestion
    {
        [SerializeField]
        private string confrontationId;

        [SerializeField]
        private int stepIndex;

        [SerializeField]
        [TextArea(2, 5)]
        private string systemStatementCn;

        [SerializeField]
        [TextArea(2, 5)]
        private string playerGoalCn;

        [SerializeField]
        private string correctEvidenceChainId;

        [SerializeField]
        private string selectableEvidenceChainIds;

        [SerializeField]
        [TextArea(2, 5)]
        private string successReplyCn;

        [SerializeField]
        [TextArea(2, 5)]
        private string failReplyCn;

        [SerializeField]
        private bool allowRetry;

        public string ConfrontationId => confrontationId;
        public int StepIndex => stepIndex;
        public string SystemStatementCn => systemStatementCn;
        public string PlayerGoalCn => playerGoalCn;
        public string CorrectEvidenceChainId => correctEvidenceChainId;
        public string SelectableEvidenceChainIds => selectableEvidenceChainIds;
        public string SuccessReplyCn => successReplyCn;
        public string FailReplyCn => failReplyCn;
        public bool AllowRetry => allowRetry;

        public FinalConfrontationQuestion(
            string confrontationId,
            int stepIndex,
            string systemStatementCn,
            string playerGoalCn,
            string correctEvidenceChainId,
            string selectableEvidenceChainIds,
            string successReplyCn,
            string failReplyCn,
            bool allowRetry)
        {
            this.confrontationId = confrontationId;
            this.stepIndex = stepIndex;
            this.systemStatementCn = systemStatementCn;
            this.playerGoalCn = playerGoalCn;
            this.correctEvidenceChainId = correctEvidenceChainId;
            this.selectableEvidenceChainIds = selectableEvidenceChainIds;
            this.successReplyCn = successReplyCn;
            this.failReplyCn = failReplyCn;
            this.allowRetry = allowRetry;
        }

        public bool IsCorrect(string selectedChainId)
        {
            return !string.IsNullOrWhiteSpace(selectedChainId) &&
                   string.Equals(
                       selectedChainId,
                       correctEvidenceChainId,
                       StringComparison.Ordinal);
        }
    }

    [CreateAssetMenu(
        fileName = "FinalConfrontationDatabase",
        menuName = "MingBay/Data/Final Confrontation Database")]
    public sealed class FinalConfrontationDatabase : ScriptableObject
    {
        [SerializeField]
        private List<FinalConfrontationQuestion> questions = new();

        [SerializeField]
        private List<FinalEvidenceOption> evidenceOptions = new();

        public IReadOnlyList<FinalConfrontationQuestion> Questions => questions;
        public IReadOnlyList<FinalEvidenceOption> EvidenceOptions => evidenceOptions;

        public FinalEvidenceOption GetEvidenceOption(string chainId)
        {
            if (string.IsNullOrWhiteSpace(chainId) || evidenceOptions == null)
            {
                return null;
            }

            foreach (FinalEvidenceOption option in evidenceOptions)
            {
                if (option != null &&
                    string.Equals(option.ChainId, chainId, StringComparison.Ordinal))
                {
                    return option;
                }
            }

            return null;
        }

        public void ReplaceContent(
            IEnumerable<FinalConfrontationQuestion> newQuestions,
            IEnumerable<FinalEvidenceOption> newEvidenceOptions)
        {
            questions = newQuestions != null
                ? new List<FinalConfrontationQuestion>(newQuestions)
                : new List<FinalConfrontationQuestion>();
            questions.Sort((left, right) =>
                (left?.StepIndex ?? 0).CompareTo(right?.StepIndex ?? 0));

            evidenceOptions = newEvidenceOptions != null
                ? new List<FinalEvidenceOption>(newEvidenceOptions)
                : new List<FinalEvidenceOption>();
        }
    }
}
