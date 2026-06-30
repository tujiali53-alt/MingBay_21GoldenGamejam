using System.Collections.Generic;
using MingBay.Data;
using UnityEngine;

namespace MingBay.Core
{
    /// <summary>
    /// Stores whole-run metrics across scene changes.
    /// Local scene managers can still keep their per-level counters for UI,
    /// while EndingScene reads this persisted aggregate.
    /// </summary>
    public static class GameRunState
    {
        private const string ResolvedCountKey = "MingBay.Run.ResolvedCount";
        private const string TransferCountKey = "MingBay.Run.TransferCount";
        private const string UserSatisfactionKey = "MingBay.Run.UserSatisfaction";
        private const string EvidenceIdsKey = "MingBay.Run.EvidenceIds";
        private const string A07RiskKey = "MingBay.Run.A07Risk";
        private const string AutoClearCountKey = "MingBay.Run.AutoClearCount";
        private const string FinalCorrectCountKey = "MingBay.Run.FinalCorrectCount";
        private const char EvidenceSeparator = '|';

        public static void ResetRun()
        {
            PlayerPrefs.SetInt(ResolvedCountKey, 0);
            PlayerPrefs.SetInt(TransferCountKey, 0);
            PlayerPrefs.SetInt(UserSatisfactionKey, 0);
            PlayerPrefs.SetInt(A07RiskKey, 0);
            PlayerPrefs.SetInt(AutoClearCountKey, 0);
            PlayerPrefs.SetInt(FinalCorrectCountKey, 0);
            PlayerPrefs.SetString(EvidenceIdsKey, string.Empty);
            PlayerPrefs.Save();
        }

        public static void ResetFinalConfrontation()
        {
            PlayerPrefs.SetInt(FinalCorrectCountKey, 0);
            PlayerPrefs.Save();
        }

        public static void Apply(MetricDelta delta)
        {
            PlayerPrefs.SetInt(
                ResolvedCountKey,
                PlayerPrefs.GetInt(ResolvedCountKey, 0) + delta.ResolvedCount);
            PlayerPrefs.SetInt(
                TransferCountKey,
                PlayerPrefs.GetInt(TransferCountKey, 0) + delta.TransferCount);
            PlayerPrefs.SetInt(
                UserSatisfactionKey,
                PlayerPrefs.GetInt(UserSatisfactionKey, 0) + delta.UserSatisfaction);
            PlayerPrefs.SetInt(
                A07RiskKey,
                PlayerPrefs.GetInt(A07RiskKey, 0) + delta.A07Risk);
            PlayerPrefs.Save();
        }

        public static void RecordAutoClear()
        {
            PlayerPrefs.SetInt(
                AutoClearCountKey,
                PlayerPrefs.GetInt(AutoClearCountKey, 0) + 1);
            PlayerPrefs.Save();
        }

        public static void RecordFinalAnswer(bool isCorrect)
        {
            if (!isCorrect)
            {
                return;
            }

            PlayerPrefs.SetInt(
                FinalCorrectCountKey,
                PlayerPrefs.GetInt(FinalCorrectCountKey, 0) + 1);
            PlayerPrefs.Save();
        }

        public static bool SaveEvidence(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
            {
                return false;
            }

            HashSet<string> evidenceIds = LoadEvidenceIds();
            if (!evidenceIds.Add(evidenceId))
            {
                return false;
            }

            PlayerPrefs.SetString(EvidenceIdsKey, string.Join(EvidenceSeparator, evidenceIds));
            PlayerPrefs.Save();
            return true;
        }

        public static EndingMetrics GetEndingMetrics()
        {
            return new EndingMetrics(
                PlayerPrefs.GetInt(ResolvedCountKey, 0),
                PlayerPrefs.GetInt(TransferCountKey, 0),
                PlayerPrefs.GetInt(UserSatisfactionKey, 0),
                LoadEvidenceIds().Count,
                PlayerPrefs.GetInt(A07RiskKey, 0),
                PlayerPrefs.GetInt(AutoClearCountKey, 0),
                PlayerPrefs.GetInt(FinalCorrectCountKey, 0));
        }

        public static IReadOnlyCollection<string> GetUnlockedEvidenceIds()
        {
            return LoadEvidenceIds();
        }

        private static HashSet<string> LoadEvidenceIds()
        {
            HashSet<string> evidenceIds = new();
            string raw = PlayerPrefs.GetString(EvidenceIdsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return evidenceIds;
            }

            string[] tokens = raw.Split(EvidenceSeparator);
            foreach (string token in tokens)
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    evidenceIds.Add(token);
                }
            }

            return evidenceIds;
        }
    }
}
