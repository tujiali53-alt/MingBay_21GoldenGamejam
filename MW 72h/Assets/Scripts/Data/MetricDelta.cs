using System;
using UnityEngine;

namespace MingBay.Data
{
    /// <summary>
    /// 一次玩家选择造成的指标变化。
    /// 所有分支使用同一结构，方便策划直接在 Inspector 中调整数值。
    /// </summary>
    [Serializable]
    public struct MetricDelta
    {
        [SerializeField]
        [InspectorName("已解决变化")]
        [Tooltip("该选择对已解决工单数量的影响。通常完成工单时填写 1。")]
        private int resolvedCount;

        [SerializeField]
        [InspectorName("人工转接变化")]
        [Tooltip("该选择对人工转接次数的影响。点击“转人工”时通常填写 1。")]
        private int transferCount;

        [SerializeField]
        [InspectorName("用户满意度变化")]
        [Tooltip("该选择对用户满意度的影响，可以填写负数。")]
        private int userSatisfaction;

        [SerializeField]
        [InspectorName("A-07 风险变化")]
        [Tooltip("该选择对 A-07 风险值的影响，可以填写负数。")]
        private int a07Risk;

        public int ResolvedCount => resolvedCount;
        public int TransferCount => transferCount;
        public int UserSatisfaction => userSatisfaction;
        public int A07Risk => a07Risk;

        public static MetricDelta operator +(MetricDelta left, MetricDelta right)
        {
            return new MetricDelta(
                left.resolvedCount + right.resolvedCount,
                left.transferCount + right.transferCount,
                left.userSatisfaction + right.userSatisfaction,
                left.a07Risk + right.a07Risk);
        }

        public MetricDelta(
            int resolvedCount,
            int transferCount,
            int userSatisfaction,
            int a07Risk)
        {
            this.resolvedCount = resolvedCount;
            this.transferCount = transferCount;
            this.userSatisfaction = userSatisfaction;
            this.a07Risk = a07Risk;
        }
    }

    /// <summary>
    /// 当前整局游戏的指标快照，供 UI 和后续结局系统读取。
    /// </summary>
    public readonly struct GameMetrics
    {
        public int ResolvedCount { get; }
        public int TransferCount { get; }
        public int UserSatisfaction { get; }
        public int EvidenceCount { get; }
        public int A07Risk { get; }

        public GameMetrics(
            int resolvedCount,
            int transferCount,
            int userSatisfaction,
            int evidenceCount,
            int a07Risk)
        {
            ResolvedCount = resolvedCount;
            TransferCount = transferCount;
            UserSatisfaction = userSatisfaction;
            EvidenceCount = evidenceCount;
            A07Risk = a07Risk;
        }
    }
}
