using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    /// <summary>
    /// An object instance on the heap, maintaining its field mappings.
    /// </summary>
    internal sealed class HeapNode
    {
        public AllocationNode Allocation { get; }
        public ImmutableDictionary<AccessSymbol, IAbstractValue> Fields { get; }

        public HeapNode(AllocationNode allocation, ImmutableDictionary<AccessSymbol, IAbstractValue> fields)
        {
            Allocation = allocation ?? throw new ArgumentNullException(nameof(allocation));
            Fields = fields?.WithComparers(AccessSymbol.EqualityComparer) ?? throw new ArgumentNullException(nameof(fields));
        }

        public static HeapNode Create(AllocationNode allocation) => new HeapNode(allocation, ImmutableDictionary<AccessSymbol, IAbstractValue>.Empty);
        public IAbstractValue? TryGetField(AccessSymbol field)
        {
            if (Fields.TryGetValue(field, out var pointsTo))
                return pointsTo;
            return null;
        }
        public HeapNode SetField(AccessSymbol field, IAbstractValue targetSet)
        {
            return new HeapNode(Allocation, Fields.SetItem(field, targetSet));
        }
    }

}
