using Abaddax.Roslyn.Analyzers.Framework.Analysis.ExpressionOrigin.Models;
using Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow;
using Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow.Models;
using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.ExpressionOrigin
{
    public static class ExpressionOriginAnalysis
    {
        /// <summary>
        /// Tries to unwrap forward variable declarations and assignments back to the last conclusive assignment
        /// </summary>
        /// <returns>Last conclusive assignment or <paramref name="expression"/></returns>
        public static ExpressionSyntax TraverseAssignments(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            TraverseCallback? onTraverseCallback = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetSymbol = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol;
            if (targetSymbol == null)
                return expression;

            var operation = semanticModel.GetOperation(expression, cancellationToken);
            if (operation == null)
                return expression;

            var lastAssignment = MethodFlowAnalysis.TraverseAssignments(operation, semanticModel, cancellationToken,
                onTraverseCallback: onTraverseCallback);
            if (lastAssignment == null || expression == lastAssignment.Syntax)
                return expression;

            var lastAssignmentExpression = lastAssignment.Syntax switch
            {
                // Special case for foreach(var x in y) -> forward to y
                IdentifierNameSyntax identifier when
                    identifier.Parent is ForEachStatementSyntax foreachLoop
                    => foreachLoop.Expression,
                _ => lastAssignment.Syntax as ExpressionSyntax
            };
            if (lastAssignmentExpression == null)
                return expression;

            return TraverseAssignments(lastAssignmentExpression, semanticModel, cancellationToken,
                onTraverseCallback: onTraverseCallback);
        }

        /// <summary>
        /// Tries to expand an expression to its full path
        /// </summary>
        /// <remarks>Example: var x = new Prop(); var y = x.A; var z = y.B; -> z = new Prop().A.B</remarks>
        /// <returns></returns>
        public static Models.ExpressionOrigin? TryExpand(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            TraverseCallback? onTraverseCallback = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Unwrap forwarded variable assignments. var x = Func(); var y = x -> y = Func()
            expression = TraverseAssignments(expression, semanticModel, cancellationToken,
                onTraverseCallback: onTraverseCallback)
                .IgnoreCasts()
                .IgnoreNullSuppression();

            switch (expression)
            {
                // Unwrap nested property access
                case MemberAccessExpressionSyntax memberAccess:
                {
                    var receiver = TryExpand(memberAccess.Expression, semanticModel, cancellationToken,
                        onTraverseCallback: onTraverseCallback);
                    if (receiver == null)
                        return null;
                    var member = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
                    if (member == null)
                        return null;
                    return new MemberExpressionOrigin(receiver, member);
                }
                // Unwrap nested property access via indexer
                case ElementAccessExpressionSyntax elementAccess:
                {
                    var receiver = TryExpand(elementAccess.Expression, semanticModel, cancellationToken,
                         onTraverseCallback: onTraverseCallback);
                    return receiver;
                }
                // Unwrap awaits
                case AwaitExpressionSyntax asyncAccess:
                {
                    var receiver = TryExpand(asyncAccess.Expression, semanticModel, cancellationToken,
                        onTraverseCallback: onTraverseCallback);
                    if (receiver == null)
                        return null;
                    return new AsyncSyntaxExpressionOrigin(receiver);
                }
                // Unwrap invocations
                case InvocationExpressionSyntax invocation:
                {
                    var receiver = TryExpand(invocation.Expression, semanticModel, cancellationToken,
                        onTraverseCallback: onTraverseCallback);
                    if (receiver == null)
                        return new SyntaxExpressionOrigin(expression);
                    return new InvocationSyntaxExpressionOrigin(receiver, invocation);
                }
                default:
                    return new SyntaxExpressionOrigin(expression);
            }
        }
    }
}
