using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class ExpressionSyntaxHelper
    {
        #region Helper
        [DebuggerDisplay("{DebuggerDisplay,nq}")]
        public abstract class ExpressionOrigin
        {
            public abstract string ToFullString();

            private string DebuggerDisplay
            {
                get
                {
                    return ToFullString().Replace("\r", "").Replace("\n", "");
                }
            }
        }
        public sealed class SyntaxExpressionOrigin : ExpressionOrigin
        {
            public ExpressionSyntax Syntax { get; }
            public SyntaxExpressionOrigin(ExpressionSyntax syntax)
            {
                Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
            }
            public override string ToFullString() => Syntax.ToFullString();
        }
        public sealed class MemberExpressionOrigin : ExpressionOrigin
        {
            public ExpressionOrigin Receiver { get; }
            public ISymbol Member { get; }

            public MemberExpressionOrigin(
                ExpressionOrigin receiver,
                ISymbol member)
            {
                Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
                Member = member ?? throw new ArgumentNullException(nameof(member));
            }
            public override string ToFullString()
            {
                return Receiver.ToFullString() + "." + Member.Name;
            }
        }
        public abstract class ForwardingExpressionOrigin : ExpressionOrigin
        {
            public ExpressionOrigin Receiver { get; }
            public ForwardingExpressionOrigin(ExpressionOrigin receiver)
            {
                Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            }
        }
        public sealed class AsyncSyntaxExpressionOrigin : ForwardingExpressionOrigin
        {
            public AsyncSyntaxExpressionOrigin(ExpressionOrigin receiver)
                : base(receiver) { }
            public override string ToFullString()
            {
                return "await " + Receiver.ToFullString();
            }
        }
        public sealed class InvocationSyntaxExpressionOrigin : ForwardingExpressionOrigin
        {
            public InvocationExpressionSyntax Invocation { get; }
            public InvocationSyntaxExpressionOrigin(ExpressionOrigin receiver, InvocationExpressionSyntax invocation)
                : base(receiver)
            {
                Invocation = invocation ?? throw new ArgumentNullException(nameof(invocation));
            }
            public override string ToFullString()
            {
                return Receiver.ToFullString() + Invocation.ArgumentList.ToFullString();
            }
        }
        #endregion

        /// <summary>
        /// Tries to unwrap forward variable declarations and assignments back to the last conclusiv assignment
        /// </summary>
        /// <returns>Last conclusiv assignment or <paramref name="expression"/></returns>
        public static ExpressionSyntax TraverseAssignments(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var methodBody = expression.GetContainingMethodDeclarationBlock();
            if (methodBody == null)
                return expression;

            var targetSymbol = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol;
            if (targetSymbol == null)
                return expression;

            var lastAssignment = AssignmentHelper.GetDefinitiveLastAssignment(
                methodBody,
                expression.IgnoreCasts().IgnoreNullSuppression(),
                targetSymbol,
                semanticModel,
                cancellationToken);
            if (lastAssignment.Type != AssignmentHelper.LastAssignment.MatchType.Found || lastAssignment.Assignment == null)
                return expression;

            var lastAssignmentExpression = lastAssignment.Assignment.Syntax switch
            {
                IdentifierNameSyntax identifier when identifier.Parent is ForEachStatementSyntax foreachLoop => foreachLoop.Expression,
                AssignmentExpressionSyntax assignment => assignment.Right,
                VariableDeclaratorSyntax varDecl => varDecl.Initializer?.Value,
                _ => null
            };
            if (lastAssignmentExpression == null)
                return expression;

            return TraverseAssignments(lastAssignmentExpression, semanticModel, cancellationToken);
        }

        /// <summary>
        /// Tries to expand an expression to its full path
        /// </summary>
        /// <remarks>Example: var x = new Prop(); var y = x.A; var z = y.B; -> z = new Prop().A.B</remarks>
        /// <returns></returns>
        public static ExpressionOrigin? TryExpand(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //Unwrap forwarded variable assignements. var x = Func(); var y = x -> y = Func()
            expression = TraverseAssignments(expression, semanticModel, cancellationToken)
                .IgnoreCasts()
                .IgnoreNullSuppression();

            switch (expression)
            {
                //Unwrap nested property access
                case MemberAccessExpressionSyntax memberAccess:
                {
                    var receiver = TryExpand(
                        memberAccess.Expression,
                        semanticModel,
                        cancellationToken);
                    if (receiver == null)
                        return null;
                    var member = semanticModel.GetSymbolInfo(memberAccess, cancellationToken)
                       .Symbol;
                    if (member == null)
                        return null;
                    return new MemberExpressionOrigin(receiver, member);
                }
                //Unwrap nested property access via indexer
                case ElementAccessExpressionSyntax elementAccess:
                {
                    var receiver = TryExpand(
                         elementAccess.Expression,
                         semanticModel,
                         cancellationToken);
                    return receiver;
                }
                //Unwrap awaits
                case AwaitExpressionSyntax asyncAccess:
                {
                    var receiver = TryExpand(
                       asyncAccess.Expression,
                       semanticModel,
                       cancellationToken);
                    if (receiver == null)
                        return null;
                    return new AsyncSyntaxExpressionOrigin(receiver);
                }
                //Unwrap invocations
                case InvocationExpressionSyntax invocation:
                {
                    var receiver = TryExpand(
                        invocation.Expression,
                        semanticModel,
                        cancellationToken);
                    if (receiver == null)
                        return new SyntaxExpressionOrigin(expression);
                    return new InvocationSyntaxExpressionOrigin(receiver, invocation);
                    //return receiver;
                }
                default:
                    return new SyntaxExpressionOrigin(expression);
            }
        }
    }
}
