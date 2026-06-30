using System.Collections.Generic;
using UnityEngine;

namespace MingBay.Core
{
    /// <summary>
    /// 统一管理本局已保存的证据，防止同一证据被重复计数。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("明湾/核心/证据管理器")]
    public sealed class EvidenceManager : MonoBehaviour
    {
        private readonly HashSet<string> savedEvidenceIds = new();

        public int EvidenceCount => savedEvidenceIds.Count;

        /// <summary>
        /// 保存一条新证据。证据 ID 为空或已存在时返回 false。
        /// </summary>
        public bool SaveEvidence(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId) || !savedEvidenceIds.Add(evidenceId))
            {
                return false;
            }

            GameRunState.SaveEvidence(evidenceId);
            return true;
        }

        /// <summary>
        /// 开始新一局时清空所有运行时证据。
        /// </summary>
        public void ResetEvidence()
        {
            savedEvidenceIds.Clear();
        }
    }
}
