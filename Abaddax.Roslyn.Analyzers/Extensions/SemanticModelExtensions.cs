using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class SemanticModelExtensions
    {
        public static bool HasSameSyntaxTree(this SemanticModel semanticModel, SyntaxNode syntaxNode)
        {
            return semanticModel.SyntaxTree == syntaxNode.SyntaxTree;
        }
        public static SemanticModel GetSemanticModelFor(this SemanticModel semanticModel, SyntaxNode syntaxNode)
        {
            if (semanticModel.HasSameSyntaxTree(syntaxNode))
                return semanticModel;
            return semanticModel.Compilation.GetSemanticModel(syntaxNode.SyntaxTree);
        }
    }
}
