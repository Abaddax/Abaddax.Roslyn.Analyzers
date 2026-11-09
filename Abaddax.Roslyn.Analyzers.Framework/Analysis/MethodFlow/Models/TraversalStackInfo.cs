using Abaddax.Roslyn.Analyzers.Framework.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow.Models
{
    internal readonly struct TraversalStackInfo
    {
        public SyntaxNode MethodSyntax { get; }
        public IMethodSymbol Method { get; }
        public SemanticModel PreviousSemanticModel { get; }
        public IReadOnlyDictionary<IParameterSymbol, ParameterValue>? PreviousParameters { get; }
        public ControlFlowGraph? PreviousControlFlowGraph { get; }

        public TraversalStackInfo(
            SyntaxNode methodSyntax,
            IMethodSymbol method,
            SemanticModel previousSemanticModel,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? previousParameters,
            ControlFlowGraph? previousControlFlowGraph)
        {
            MethodSyntax = methodSyntax ?? throw new ArgumentNullException(nameof(methodSyntax));
            Method = method ?? throw new ArgumentNullException(nameof(method));
            PreviousSemanticModel = previousSemanticModel ?? throw new ArgumentNullException(nameof(previousSemanticModel));
            PreviousParameters = previousParameters;
            PreviousControlFlowGraph = previousControlFlowGraph;
        }
    }
}
