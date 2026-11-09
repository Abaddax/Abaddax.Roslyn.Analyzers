using Abaddax.Roslyn.Analyzers.Framework.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    internal readonly struct VariableFlowAnalysisContext
    {
        public required ControlFlowGraph CFG { get; init; }
        public required SemanticModel SemanticModel { get; init; }
        public required IReadOnlyDictionary<IParameterSymbol, ParameterValue> KnownArguments { get; init; }
        public required ISet<IMethodSymbol> CallStack { get; init; }
        public required ushort MaxCallStackDepth { get; init; }
        public required ProgramState EmptyState { get; init; }

    }
}
