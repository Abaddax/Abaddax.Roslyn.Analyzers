using Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Extensions;
using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Abaddax.Roslyn.Analyzers.Framework.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    /// <summary>
    /// Precomputed flow-state of the given CFG
    /// </summary>
    public sealed class VariableFlowState
    {
        internal ControlFlowGraph CFG { get; }
        internal ImmutableDictionary<IOperation, ProgramState> States { get; }
        internal ImmutableArray<ProgramState> ExitStates { get; }

        internal VariableFlowState(ControlFlowGraph cfg, IReadOnlyDictionary<IOperation, ProgramState> states, IEnumerable<ProgramState> exitStates)
        {
            CFG = cfg ?? throw new ArgumentNullException(nameof(cfg));
            States = states?.ToImmutableDictionary().WithComparers(OperationEqualityComparer.Default) ?? throw new ArgumentNullException(nameof(states));
            ExitStates = exitStates?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(exitStates));
        }

        internal ProgramState? TryGetState(IOperation operation)
        {
            var cfgOperation = CFG.FindMatchingOperation(operation);
            if (cfgOperation == null)
                return null;
            while (cfgOperation != null)
            {
                if (States.TryGetValue(cfgOperation, out var methodState))
                    return methodState;
                cfgOperation = cfgOperation.Parent;
            }
            return null;
        }

        public IEnumerable<IOperation?> GetPossibleLastValues(IOperation variableReference)
        {
            var operation = CFG.FindMatchingOperation(variableReference);
            if (operation == null)
                yield break;
            var state = TryGetState(operation);
            if (state == null)
                yield break;
            var accessPath = VariableFlowAnalysis.TryBuildAccessPath(operation, out _);
            if (accessPath == null)
                yield break;
            var value = ProgramStateExtensions.EvaluatePath(ref state, accessPath.Value);
            if (value == null)
                yield break;
            foreach (var possibleValue in ProgramStateExtensions.GetPossibleLastValues(value))
            {
                // Do not return itself
                if (!OperationEqualityComparer.Default.Equals(possibleValue, operation))
                    yield return possibleValue;
            }
        }
    }
}
