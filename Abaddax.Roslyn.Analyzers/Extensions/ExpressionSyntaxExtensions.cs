using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class ExpressionSyntaxExtensions
    {
        public static ExpressionSyntax IgnoreCasts(this ExpressionSyntax expression)
        {
            if (expression.IgnoreNullSuppression() is CastExpressionSyntax cast)
                return IgnoreCasts(cast.Expression);
            if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AsExpression))
                return IgnoreCasts(binary.Left);
            return expression;
        }
        public static ExpressionSyntax IgnoreNullSuppression(this ExpressionSyntax expression)
        {
            if (expression is PostfixUnaryExpressionSyntax postFixUnary && postFixUnary.Kind() == SyntaxKind.SuppressNullableWarningExpression)
                return IgnoreNullSuppression(postFixUnary.Operand);
            return expression;
        }

        public static MethodDeclarationSyntax? GetContainingMethodDeclarationBlock(this ExpressionSyntax expression)
        {
            return expression
                .Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();
        }
    }
}
