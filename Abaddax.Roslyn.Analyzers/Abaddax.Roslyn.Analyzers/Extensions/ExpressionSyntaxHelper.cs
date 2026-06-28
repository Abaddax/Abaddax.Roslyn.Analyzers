using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class ExpressionSyntaxHelper
    {
        #region Helper
        public abstract class ExpressionOrigin
        {
            public abstract string ToFullString();
        }
        public class SyntaxExpressionOrigin : ExpressionOrigin
        {
            public ExpressionSyntax Syntax { get; }
            public SyntaxExpressionOrigin(ExpressionSyntax syntax)
            {
                Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
            }
            public override string ToFullString() => Syntax.ToFullString();
        }
        public class AsyncSyntaxExpressionOrigin : ExpressionOrigin
        {
            public ExpressionOrigin Receiver { get; }
            public AsyncSyntaxExpressionOrigin(ExpressionOrigin receiver)
            {
                Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            }
            public override string ToFullString()
            {
                return "await " + Receiver.ToFullString();
            }
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
        #endregion

        public static ExpressionSyntax TraverseAssignments(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (expression.IgnoreCasts().IgnoreNullSuppression() is not IdentifierNameSyntax identifier)
                return expression;
            var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is not ILocalSymbol localSymbol)
                return expression;

            var cfg = GetControlFlowGraph(
                identifier,
                semanticModel,
                cancellationToken);
            if (cfg == null)
                return identifier;

            var definitions = FindDefinitions(
                cfg,
                localSymbol,
                expression,
                cancellationToken);
            if (definitions.Count != 1)
                return expression;
            if (definitions[0].Value.Syntax is not ExpressionSyntax value)
                return expression;
            return TraverseAssignments(
                value,
                semanticModel,
                cancellationToken);
        }

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
                case IdentifierNameSyntax identifier:
                {
                    var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken)
                        .Symbol;
                    if (symbol is not ILocalSymbol)
                    {
                        return new SyntaxExpressionOrigin(identifier);
                    }
                    return TryExpand(identifier, semanticModel, cancellationToken);
                }
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
                default:
                    return new SyntaxExpressionOrigin(expression);
            }
        }

        private static List<IAssignmentOperation> FindDefinitions(
            ControlFlowGraph cfg,
            ILocalSymbol target,
            ExpressionSyntax use,
            CancellationToken cancellationToken)
        {
            var result = new HashSet<IAssignmentOperation>();

            foreach (var block in cfg.Blocks)
            {
                foreach (var operation in block.Operations)
                {
                    CollectAssignments(
                        operation,
                        target,
                        result,
                        cancellationToken);
                }
            }

            // Keep only definitions before the use
            return result
                .Where(x => x.Syntax.SpanStart < use.SpanStart)
                .ToList();
        }
        private static void CollectAssignments(
            IOperation operation,
            ILocalSymbol target,
            HashSet<IAssignmentOperation> result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (operation is IAssignmentOperation assignment)
            {
                var symbol =
                    GetAssignedSymbol(assignment.Target);
                if (SymbolEqualityComparer.Default.Equals(symbol, target))
                {
                    result.Add(assignment);
                }
            }

            foreach (var child in operation.ChildOperations)
            {
                CollectAssignments(
                    child,
                    target,
                    result,
                    cancellationToken);
            }
        }
        private static ISymbol? GetAssignedSymbol(
            IOperation operation)
        {
            if (operation is ILocalReferenceOperation local)
                return local.Local;
            return null;
        }
        private static ControlFlowGraph? GetControlFlowGraph(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var body = expression.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
            if (body == null)
                return null;

            var operation = semanticModel.GetOperation(
                body,
                cancellationToken);

            if (operation is IMethodBodyOperation methodBody)
            {
                return ControlFlowGraph.Create(methodBody, cancellationToken);
            }
            if (operation is IConstructorBodyOperation constructorBody)
            {
                return ControlFlowGraph.Create(constructorBody, cancellationToken);
            }
            return null;
        }
    }
}
