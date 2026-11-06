using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    /// <summary>
    /// The total immutable state of the program/method (Environment Stack + Heap Store).
    /// </summary>
    internal sealed class ProgramState
    {
        public static readonly ProgramState Empty = new(
            ImmutableDictionary<AccessPathRoot, IAbstractValue>.Empty,
            ImmutableDictionary<AllocationNode, HeapNode>.Empty
        );

        /// <summary>
        /// "Stack" variables
        /// </summary>
        public ImmutableDictionary<AccessPathRoot, IAbstractValue> Environment { get; }
        /// <summary>
        /// Heap allocations
        /// </summary>
        public ImmutableDictionary<AllocationNode, HeapNode> HeapStore { get; }

        public ProgramState(
            ImmutableDictionary<AccessPathRoot, IAbstractValue> environment,
            ImmutableDictionary<AllocationNode, HeapNode> heapStore)
        {
            Environment = environment?.WithComparers(AccessPathRoot.EqualityComparer) ?? throw new ArgumentNullException(nameof(environment));
            HeapStore = heapStore?.WithComparers(AllocationNode.EqualityComparer) ?? throw new ArgumentNullException(nameof(heapStore));
        }

        /// <summary>
        /// Merges two program states (e.g., at the end of an if/else block).
        /// Combines points-to sets for references and merges fields for structs.
        /// </summary>
        public static ProgramState Merge(ProgramState state1, ProgramState state2)
        {
            // Merge Environments
            var mergedEnvironment = state1.Environment.ToBuilder();
            foreach (var stackVariable2 in state2.Environment)
            {
                if (mergedEnvironment.TryGetValue(stackVariable2.Key, out var stackVariable1))
                    mergedEnvironment[stackVariable2.Key] = stackVariable1.Merge(stackVariable2.Value);
                else
                {
                    mergedEnvironment.Add(stackVariable2);
                }
            }

            // Merge Heaps
            var mergedHeap = state1.HeapStore.ToBuilder();
            foreach (var heapNode2 in state2.HeapStore)
            {
                if (mergedHeap.TryGetValue(heapNode2.Key, out var heapNode1))
                {
                    var mergedFields = heapNode1.Fields.ToBuilder();
                    foreach (var field2 in heapNode2.Value.Fields)
                    {
                        if (mergedFields.TryGetValue(field2.Key, out var field1))
                            mergedFields[field2.Key] = field1.Merge(field2.Value);
                        else
                        {
                            mergedFields.Add(field2);
                        }
                    }
                    mergedHeap[heapNode2.Key] = new HeapNode(heapNode2.Value.Allocation, mergedFields.ToImmutable());
                }
                else
                {
                    mergedHeap.Add(heapNode2);
                }
            }

            return new ProgramState(mergedEnvironment.ToImmutable(), mergedHeap.ToImmutable());
        }

        public bool IsEquivalentTo(ProgramState other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            if (Environment.Count != other.Environment.Count)
                return false;
            if (HeapStore.Count != other.HeapStore.Count)
                return false;
            foreach (var stackVariable in Environment)
            {
                if (!other.Environment.TryGetValue(stackVariable.Key, out var otherStackVariable))
                    return false;
                if (!stackVariable.Value.IsEquivalentTo(otherStackVariable))
                    return false;
            }
            foreach (var heapNode in HeapStore)
            {
                if (!other.HeapStore.TryGetValue(heapNode.Key, out var otherHeapNode))
                    return false;
                if (!AllocationNode.EqualityComparer.Equals(heapNode.Value.Allocation, otherHeapNode.Allocation))
                    return false;
                if (heapNode.Value.Fields.Count != otherHeapNode.Fields.Count)
                    return false;
                foreach (var field in heapNode.Value.Fields)
                {
                    if (!otherHeapNode.Fields.TryGetValue(field.Key, out var otherField))
                        return false;
                    if (!field.Value.IsEquivalentTo(otherField))
                        return false;
                }
            }
            return true;
        }

        public ProgramState WithEnvironment(AccessPathRoot variableSymbol, IAbstractValue variableValue)
        {
            return new ProgramState(Environment.SetItem(variableSymbol, variableValue), HeapStore);
        }
        public ProgramState WithHeap(AllocationNode allocationNode, HeapNode heapNode)
        {
            return new ProgramState(Environment, HeapStore.SetItem(allocationNode, heapNode));
        }
    }
}
