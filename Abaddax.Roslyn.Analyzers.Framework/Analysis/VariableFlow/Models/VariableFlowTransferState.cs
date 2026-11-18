using Abaddax.Roslyn.Analyzers.Framework.Helper;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    internal readonly struct VariableFlowTransferState
    {
        public static readonly VariableFlowTransferState Empty = new(
            ImmutableHashSet<AccessPath>.Empty.WithComparer(AccessPath.EqualityComparer),
            ImmutableDictionary<IOperation, ProgramState>.Empty.WithComparers(OperationEqualityComparer.Default)
        );

        public ImmutableHashSet<AccessPath> PathsWritten { get; }

        public ImmutableDictionary<IOperation, ProgramState> NestedOperationStates { get; }

        public VariableFlowTransferState(ImmutableHashSet<AccessPath> pathsWritten, ImmutableDictionary<IOperation, ProgramState> nestedOperationStates)
        {
            PathsWritten = pathsWritten?.WithComparer(AccessPath.EqualityComparer) ?? throw new ArgumentNullException(nameof(pathsWritten));
            NestedOperationStates = nestedOperationStates?.WithComparers(OperationEqualityComparer.Default) ?? throw new ArgumentNullException(nameof(nestedOperationStates));
        }

        public VariableFlowTransferState WithPathWritten(AccessPath written)
        {
            return new VariableFlowTransferState(PathsWritten.Add(written), NestedOperationStates);
        }
        public VariableFlowTransferState WithPathsWritten(IEnumerable<AccessPath> written)
        {
            return new VariableFlowTransferState(PathsWritten.Union(written), NestedOperationStates);
        }
        public VariableFlowTransferState WithNestedState(IOperation operation, ProgramState state)
        {
            if (NestedOperationStates.TryGetValue(operation, out var currentState))
            {
                state = ProgramState.Merge(currentState, state);
            }
            return new VariableFlowTransferState(PathsWritten, NestedOperationStates.Add(operation, state));
        }

        public static VariableFlowTransferState Merge(VariableFlowTransferState state1, VariableFlowTransferState state2)
        {
            var pathsWritten = state1.PathsWritten.Union(state2.PathsWritten);
            var nestedOperationStates = state1.NestedOperationStates.Concat(state2.NestedOperationStates)
                .GroupBy(x => x.Key, OperationEqualityComparer.Default)
                .Select(nestedOperations =>
                {
                    var state = ProgramState.Empty;
                    foreach (var nestedOperation in nestedOperations)
                    {
                        state = ProgramState.Merge(state, nestedOperation.Value);
                    }
                    return (nestedOperations.Key, State: state);
                })
                .ToImmutableDictionary(
                    x => x.Key,
                    y => y.State,
                    OperationEqualityComparer.Default);
            return new VariableFlowTransferState(pathsWritten, nestedOperationStates);
        }
    }
}
