using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.ExpressionOrigin.Models
{
    public sealed class SyntaxExpressionOrigin : ExpressionOrigin
    {
        public ExpressionSyntax Syntax { get; }
        public SyntaxExpressionOrigin(ExpressionSyntax syntax)
        {
            Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
        }
        public override string ToFullString() => Syntax.ToFullString();
    }
}
