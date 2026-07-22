using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class SemanticModelExtensions
    {
        public static bool HasSameSyntaxTree(this SemanticModel semanticModel, SyntaxNode syntaxNode)
        {
            return semanticModel.SyntaxTree == syntaxNode.SyntaxTree;
        }
    }
}
