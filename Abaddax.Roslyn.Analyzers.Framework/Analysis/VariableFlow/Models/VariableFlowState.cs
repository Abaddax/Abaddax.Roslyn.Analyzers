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

        /// <summary>
        /// Gets the known possible values at the state of the given <paramref name="variableReference"/> inside the method
        /// </summary>
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
        /// <summary>
        /// Gets the known possible values of the given <paramref name="variableReference"/> at the exit of the method
        /// </summary>
        public IEnumerable<IOperation?> GetPossibleExitValues(IOperation variableReference)
        {
            var accessPath = VariableFlowAnalysis.TryBuildAccessPath(variableReference, out _);
            if (accessPath == null)
                yield break;
            foreach (var exitState in ExitStates)
            {
                var state = exitState;
                var value = ProgramStateExtensions.EvaluatePath(ref state, accessPath.Value);
                if (value == null)
                    continue;
                foreach (var possibleValue in ProgramStateExtensions.GetPossibleLastValues(value))
                {
                    // Do not return itself
                    if (!OperationEqualityComparer.Default.Equals(possibleValue, variableReference))
                        yield return possibleValue;
                }
            }
        }

    }
}
