using AiDev.Core.Local.Contracts;

namespace AiDev.Core.Local.Implementation.Null;

internal sealed class NullCompactor : IContextCompactor
{
    public Result<CompactionSnapshot> Compact(LocalRuntimeState state)
        => new Ok<CompactionSnapshot>(
            new CompactionSnapshot(
                CompactSummary: string.Empty,
                Facts: [],
                OpenQuestions: [],
                EstimatedTokens: 0));
}
