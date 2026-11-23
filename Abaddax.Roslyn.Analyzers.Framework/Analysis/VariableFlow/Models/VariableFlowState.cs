using Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Extensions;
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
            return ProgramStateExtensions.TryGetState(States, CFG, operation);
        }

        /// <summary>
        /// Gets the known possible values at the state of the given <paramref name="variableReference"/> inside the method
        /// </summary>
        public IEnumerable<IOperation?> GetPossibleLastValues(IOperation variableReference)
        {
            return ProgramStateExtensions.GetPossibleLastValues(States, CFG, variableReference);
        }
        /// <summary>
        /// Gets the known possible values of the given <paramref name="variableReference"/> at the exit of the method
        /// </summary>
        public IEnumerable<IOperation?> GetPossibleExitValues(IOperation variableReference)
        {
            return ProgramStateExtensions.GetPossibleExitValues(ExitStates, variableReference);
        }

    }
}
