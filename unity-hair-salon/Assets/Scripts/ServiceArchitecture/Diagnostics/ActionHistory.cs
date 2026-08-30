using System;
using System.Collections.Generic;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>成功提交后的非权威诊断记录。</summary>
    public sealed class ActionHistoryEntry
    {
        public ActionHistoryEntry(
            ActionResult actionResult,
            CustomerPhysicalStateSnapshot prePhysical,
            CustomerPhysicalStateSnapshot postPhysical,
            CustomerMetricsSnapshot preMetrics,
            CustomerMetricsSnapshot postMetrics)
        {
            ActionResult = actionResult;
            PrePhysical = prePhysical;
            PostPhysical = postPhysical;
            PreMetrics = preMetrics;
            PostMetrics = postMetrics;
        }

        public ActionResult ActionResult { get; }
        public CustomerPhysicalStateSnapshot PrePhysical { get; }
        public CustomerPhysicalStateSnapshot PostPhysical { get; }
        public CustomerMetricsSnapshot PreMetrics { get; }
        public CustomerMetricsSnapshot PostMetrics { get; }
    }

    /// <summary>动作历史输出端口。</summary>
    public interface IActionHistorySink
    {
        void Append(ActionHistoryEntry entry);
    }

    /// <summary>EditMode测试与调试可用的内存历史实现。</summary>
    public sealed class InMemoryActionHistory : IActionHistorySink
    {
        private readonly List<ActionHistoryEntry> _entries = new List<ActionHistoryEntry>();
        public IReadOnlyList<ActionHistoryEntry> Entries => _entries;
        public void Append(ActionHistoryEntry entry) => _entries.Add(entry);
    }
}
