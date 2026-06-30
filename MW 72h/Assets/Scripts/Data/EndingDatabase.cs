using System;
using System.Collections.Generic;
using UnityEngine;

namespace MingBay.Data
{
    /// <summary>
    /// Ending judgement uses the whole-run metrics accumulated across levels.
    /// It intentionally keeps auto-clear separate from resolved count so endings
    /// can distinguish "handled" from "closed by system".
    /// </summary>
    public readonly struct EndingMetrics
    {
        public int ResolvedCount { get; }
        public int TransferCount { get; }
        public int UserSatisfaction { get; }
        public int EvidenceCount { get; }
        public int A07Risk { get; }
        public int AutoClearCount { get; }
        public int FinalCorrectCount { get; }

        public EndingMetrics(
            int resolvedCount,
            int transferCount,
            int userSatisfaction,
            int evidenceCount,
            int a07Risk,
            int autoClearCount,
            int finalCorrectCount)
        {
            ResolvedCount = resolvedCount;
            TransferCount = transferCount;
            UserSatisfaction = userSatisfaction;
            EvidenceCount = evidenceCount;
            A07Risk = a07Risk;
            AutoClearCount = autoClearCount;
            FinalCorrectCount = finalCorrectCount;
        }
    }

    [Serializable]
    public sealed class EndingDefinition
    {
        [Header("Display")]
        [SerializeField]
        private string endingId;

        [SerializeField]
        private string endingNameCn;

        [SerializeField]
        private int priority;

        [SerializeField]
        private string conditionExpression;

        [SerializeField]
        private string requiredStage;

        [SerializeField]
        [TextArea(3, 6)]
        private string endingDescCn;

        [SerializeField]
        [TextArea(1, 3)]
        private string finalScreenTextCn;

        public string EndingId => endingId;
        public string EndingNameCn => endingNameCn;
        public string TitleCn => endingNameCn;
        public string SubtitleCn => finalScreenTextCn;
        public string BodyCn => endingDescCn;
        public int Priority => priority;
        public string ConditionExpression => conditionExpression;
        public string RequiredStage => requiredStage;
        public string FinalScreenTextCn => finalScreenTextCn;

        public EndingDefinition(
            string endingId,
            int priority,
            string endingNameCn,
            string conditionExpression,
            string requiredStage,
            string endingDescCn,
            string finalScreenTextCn)
        {
            this.endingId = endingId;
            this.priority = priority;
            this.endingNameCn = endingNameCn;
            this.conditionExpression = conditionExpression;
            this.requiredStage = requiredStage;
            this.endingDescCn = endingDescCn;
            this.finalScreenTextCn = finalScreenTextCn;
        }

        public bool Matches(EndingMetrics metrics)
        {
            if (string.IsNullOrWhiteSpace(conditionExpression))
            {
                return true;
            }

            string[] clauses = conditionExpression.Split(
                new[] { " AND " },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string clause in clauses)
            {
                if (!EvaluateClause(clause.Trim(), metrics))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateClause(string clause, EndingMetrics metrics)
        {
            string[] operators = { ">=", "<=", "==", "=", ">", "<" };
            foreach (string comparisonOperator in operators)
            {
                int operatorIndex =
                    clause.IndexOf(comparisonOperator, StringComparison.Ordinal);
                if (operatorIndex < 0)
                {
                    continue;
                }

                string left = clause[..operatorIndex].Trim();
                string right = clause[(operatorIndex + comparisonOperator.Length)..].Trim();
                if (!TryGetMetricValue(left, metrics, out int metricValue) ||
                    !int.TryParse(right, out int expectedValue))
                {
                    return false;
                }

                return comparisonOperator switch
                {
                    ">=" => metricValue >= expectedValue,
                    "<=" => metricValue <= expectedValue,
                    ">" => metricValue > expectedValue,
                    "<" => metricValue < expectedValue,
                    "=" or "==" => metricValue == expectedValue,
                    _ => false
                };
            }

            return false;
        }

        private static bool TryGetMetricValue(
            string fieldName,
            EndingMetrics metrics,
            out int value)
        {
            switch (fieldName)
            {
                case "AutoClearCount_Total":
                case "AutoClearCount":
                    value = metrics.AutoClearCount;
                    return true;
                case "FinalCorrectCount":
                    value = metrics.FinalCorrectCount;
                    return true;
                case "ResolvedCount":
                    value = metrics.ResolvedCount;
                    return true;
                case "TransferCount":
                    value = metrics.TransferCount;
                    return true;
                case "EvidenceCount":
                    value = metrics.EvidenceCount;
                    return true;
                case "A07Risk":
                    value = metrics.A07Risk;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }
    }

    [CreateAssetMenu(fileName = "EndingDatabase", menuName = "MingBay/Data/Ending Database")]
    public sealed class EndingDatabase : ScriptableObject
    {
        [SerializeField]
        private List<EndingDefinition> endings = new();

        public IReadOnlyList<EndingDefinition> Endings => endings;

        public EndingDefinition Resolve(EndingMetrics metrics)
        {
            EndingDefinition bestMatch = null;
            int bestPriority = int.MaxValue;

            foreach (EndingDefinition ending in endings)
            {
                if (ending == null)
                {
                    continue;
                }

                if (!ending.Matches(metrics))
                {
                    continue;
                }

                if (ending.Priority <= bestPriority)
                {
                    bestPriority = ending.Priority;
                    bestMatch = ending;
                }
            }

            return bestMatch;
        }

        public void ReplaceEndings(IEnumerable<EndingDefinition> newEndings)
        {
            endings = newEndings != null
                ? new List<EndingDefinition>(newEndings)
                : new List<EndingDefinition>();
        }
    }
}
