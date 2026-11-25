using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Abaddax.Roslyn.Analyzers.Framework.Extensions
{
    public static class ExpressionSyntaxExtensions
    {
        /// <summary>
        /// Ignores/Unwraps casts and null-suppressions like '<c>((object)x!)!</c>' and gets the inner expression '<c>x</c>'
        /// </summary>
        public static ExpressionSyntax IgnoreCastsAndNullSuppressions(this ExpressionSyntax expression)
        {
            // Null-suppression might surround the entire cast like '((object)x)!'
            return expression.IgnoreNullSuppressions().IgnoreCasts();
        }
        /// <summary>
        /// Ignores/Unwraps casts like '<c>(object)x</c>' and gets the inner expression '<c>x</c>'
        /// </summary>
        public static ExpressionSyntax IgnoreCasts(this ExpressionSyntax expression)
        {
            // Casts might be wrapped in additional parentheses like ((C)x) or (x as C)
            var castExpression = expression.IgnoreParentheses();
            // Type cast might be only after a null-suppression like ((C)x!) -> unwrap null-suppression
            if (castExpression.IgnoreNullSuppressions() is CastExpressionSyntax cast)
                return IgnoreCasts(cast.Expression);
            // As operator cast might be have a unnecessary null-suppression like (x! as C) -> unwrap null-suppression for left
            if (castExpression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AsExpression))
                return IgnoreCasts(binary.Left).IgnoreNullSuppressions();
            return expression;
        }
        /// <summary>
        /// Ignores/Unwraps null-suppressions like '<c>x!</c>' and gets the inner expression '<c>x</c>'
        /// </summary>
        public static ExpressionSyntax IgnoreNullSuppressions(this ExpressionSyntax expression)
        {
            if (expression is PostfixUnaryExpressionSyntax postFixUnary && postFixUnary.Kind() == SyntaxKind.SuppressNullableWarningExpression)
                return IgnoreNullSuppressions(postFixUnary.Operand);
            return expression;
        }
        /// <summary>
        /// Ignores/Unwraps parentheses like '<c>(x)</c>' and gets the inner expression '<c>x</c>'
        /// </summary>
        private static ExpressionSyntax IgnoreParentheses(this ExpressionSyntax expression)
        {
            if (expression is ParenthesizedExpressionSyntax parentheses &&
                parentheses.OpenParenToken.IsKind(SyntaxKind.OpenParenToken))
            {
                return IgnoreParentheses(parentheses.Expression);
            }
            return expression;
        }

        /// <summary>
        /// Find the method block the <paramref name="expression"/> is contained in
        /// </summary>
        public static MethodDeclarationSyntax? GetContainingMethodDeclarationBlock(this SyntaxNode expression)
        {
            return expression
                .AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();
        }
        /// <summary>
        /// Find the lambda block the <paramref name="expression"/> is contained in
        /// </summary>
        public static LambdaExpressionSyntax? GetContainingLambdaDefinitionBlock(this SyntaxNode expression)
        {
            return expression
                .AncestorsAndSelf()
                .OfType<LambdaExpressionSyntax>()
                .FirstOrDefault();
        }

        public static IMethodSymbol? GetContainingMethodSymbol(this SyntaxNode expression, SemanticModel semanticModel,
            CancellationToken cancellationToken = default)
        {
            var lambda = expression.GetContainingLambdaDefinitionBlock();
            if (lambda != null)
            {
                return semanticModel.GetSymbolInfo(lambda, cancellationToken).Symbol as IMethodSymbol;
            }
            var methodDecl = expression.GetContainingMethodDeclarationBlock();
            if (methodDecl != null)
            {
                return semanticModel.GetDeclaredSymbol(methodDecl, cancellationToken);
            }
            return null;
        }

    }
}
