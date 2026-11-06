using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    /// <summary>
    /// A set of allocation nodes.
    /// </summary>
    /// <remarks>
    /// Essential for handling multiple parents, control-flow merges, and ambiguous points-to targets.
    /// </remarks>
    internal readonly struct PointsToSet
    {
        public static readonly PointsToSet Empty = new(ImmutableHashSet<AllocationNode>.Empty);

        public ImmutableHashSet<AllocationNode> Nodes { get; } = ImmutableHashSet<AllocationNode>.Empty;
        public bool IsEmpty => Nodes.IsEmpty;

        public PointsToSet(ImmutableHashSet<AllocationNode> nodes)
        {
            Nodes = nodes?.WithComparer(AllocationNode.EqualityComparer) ?? throw new ArgumentNullException(nameof(nodes));
        }

        public PointsToSet Add(AllocationNode node) => new(Nodes.Add(node));
        public PointsToSet Union(PointsToSet other) => new(Nodes.Union(other.Nodes));

        public override string ToString() => $"{{{string.Join(", ", Nodes)}}}";
    }
}
