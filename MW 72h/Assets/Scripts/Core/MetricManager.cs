using MingBay.Data;
using UnityEngine;

namespace MingBay.Core
{
    /// <summary>
    /// 统一记录白板流程中的四项运行时指标。
    /// 证据数量仍由 EvidenceManager 管理，读取快照时再合并。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("明湾/核心/指标管理器")]
    public sealed class MetricManager : MonoBehaviour
    {
        private int resolvedCount;
        private int transferCount;
        private int userSatisfaction;
        private int a07Risk;

        /// <summary>
        /// 开始新游戏时清空所有指标。
        /// </summary>
        public void ResetMetrics()
        {
            resolvedCount = 0;
            transferCount = 0;
            userSatisfaction = 0;
            a07Risk = 0;
        }

        /// <summary>
        /// 应用一次选择造成的指标变化。
        /// </summary>
        public void Apply(MetricDelta delta)
        {
            resolvedCount += delta.ResolvedCount;
            transferCount += delta.TransferCount;
            userSatisfaction += delta.UserSatisfaction;
            a07Risk += delta.A07Risk;
        }

        /// <summary>
        /// 合并证据数量并返回当前完整指标。
        /// </summary>
        public GameMetrics GetSnapshot(int evidenceCount)
        {
            return new GameMetrics(
                resolvedCount,
                transferCount,
                userSatisfaction,
                evidenceCount,
                a07Risk);
        }
    }
}
