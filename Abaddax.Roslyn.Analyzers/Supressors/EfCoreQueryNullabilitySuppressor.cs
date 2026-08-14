using Abaddax.Roslyn.Analyzers.Extensions;
using Abaddax.Roslyn.Analyzers.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using static Abaddax.Roslyn.Analyzers.Helper.ExpressionSyntaxHelper;

namespace Abaddax.Roslyn.Analyzers.Supressors
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EfCoreQueryNullabilitySuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor[] _NullDereferences = new string[]
            {
                "CS8602", // Dereference of a possibly null reference.
                "CS8604", // Possible null reference argument.
                "CS8622", // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
                "CS8634", // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'class' constraint.
            }.Select(x => new SuppressionDescriptor(
                id: AnalyzerIdentifiers.EfCoreQueryNullReferenceSuppression,
                suppressedDiagnosticId: x,
                justification: "EF Core LINQ query translation handles nullability."))
            .ToArray();

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; }
            = ImmutableArray.CreateRange(_NullDereferences);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                if (!SupportedSuppressions.Any(x => x.SuppressedDiagnosticId == diagnostic.Id))
                    continue;

                ReportSuppression(diagnostic, context);
            }
        }
        private void ReportSuppression(Diagnostic diagnostic, SuppressionAnalysisContext context)
        {
            // Find the node that triggered the nullability warning
            var tree = diagnostic.Location.SourceTree;
            if (tree == null)
                return;

            var options = context.Options.GetGlobalOptions(tree);
            if (!options.IsEnabled(AnalyzerIdentifiers.EfCoreQueryNullReferenceSuppression, defaultValue: false))
                return;

            var root = tree.GetRoot(context.CancellationToken);
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not ExpressionSyntax expression)
                return;

            var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation == null)
                return;

            var semanticModel = context.GetSemanticModel(tree);

            if (IsInsideQuery(invocation, semanticModel, context.CancellationToken, out var queryInvocation))
            {
                if (IsCalledFromDbContext(queryInvocation, semanticModel, context.CancellationToken))
                {
                    if (IsQueryDelegateParameter(queryInvocation, expression, semanticModel, context.CancellationToken))
                    {

                        var descriptor = SupportedSuppressions
                            .First(x => x.SuppressedDiagnosticId == diagnostic.Id);
                        context.ReportSuppression(
                            Suppression.Create(descriptor, diagnostic));
                    }
                }
            }
        }

        private static bool IsInsideQuery(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            out InvocationExpressionSyntax queryInvocation)
        {
            queryInvocation = invocation;
            SyntaxNode? current = invocation;
            while (current != null)
            {
                // Only check inside the current method
                if (current is MethodDeclarationSyntax)
                    return false;
                if (current is InvocationExpressionSyntax currentInvocation &&
                    IsQueryInvocation(currentInvocation, semanticModel, cancellationToken))
                {
                    queryInvocation = currentInvocation;
                    return true;
                }

                // If we are currenly a nested call inside the actual query expression also check parent invocations
                current = current.Parent;
            }
            return false;

            static bool IsQueryInvocation(
                InvocationExpressionSyntax invocation,
                SemanticModel semanticModel,
                CancellationToken cancellationToken)
            {
                if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol)
                    return false;

                if (!methodSymbol.IsExtensionMethod)
                    return false;

                var extensionClass = methodSymbol.ContainingType;
                if (extensionClass.HasName("EntityFrameworkQueryableExtensions", "Microsoft.EntityFrameworkCore"))
                    return true;
                if (extensionClass.HasName("Queryable", "System.Linq"))
                    return true;
                return false;
            }
        }
        private static bool IsCalledFromDbContext(
          InvocationExpressionSyntax queryInvocation,
          SemanticModel semanticModel,
          CancellationToken cancellationToken)
        {
            // Expand the caller chain and check if the query originates from a DbContext.DbSet<T>
            var origin = ExpressionSyntaxHelper.TryExpand(queryInvocation, semanticModel, cancellationToken);
            bool dbSetFound = false;
            while (origin is not SyntaxExpressionOrigin)
            {
                switch (origin)
                {
                    case InvocationSyntaxExpressionOrigin invocationOrigin:
                    {
                        if (invocationOrigin.Invocation.Expression is MemberAccessExpressionSyntax methodAccess)
                        {
                            var symbol = semanticModel.GetSymbolInfo(methodAccess, cancellationToken).Symbol;
                            if (symbol is IMethodSymbol method)
                            {
                                // Unsupported if 'AsQueryable' is called somewhere in the chain
                                if (method.HasName("AsQueryable", "System.Linq", "Queryable"))
                                {
                                    return false;
                                }
                            }
                        }
                        origin = invocationOrigin.Receiver;
                        break;
                    }
                    case MemberExpressionOrigin parentMemberAccess:
                    {
                        if (parentMemberAccess.Member is IPropertySymbol property)
                        {
                            if (!dbSetFound)
                                dbSetFound = property.Type.IsDbSet();
                            else if (property.Type.IsDbContext()) //In case DbContext is also nested in some class
                                return true;
                        }
                        else
                        {
                            dbSetFound = false;
                        }
                        origin = parentMemberAccess.Receiver;
                        break;
                    }
                    case ForwardingExpressionOrigin forwarding:
                    {
                        origin = forwarding.Receiver;
                        continue;
                    }
                    default:
                    {
                        break;
                    }
                }
            }
            if (origin is not SyntaxExpressionOrigin expressionRoot || !dbSetFound)
                return false;
            var type = semanticModel.GetTypeInfo(expressionRoot.Syntax, cancellationToken).Type;
            if (type == null)
                return false;
            return type.IsDbContext();
        }
        private static bool IsQueryDelegateParameter(
            InvocationExpressionSyntax queryInvocation,
            ExpressionSyntax lambdaExpression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (semanticModel.GetOperation(queryInvocation, cancellationToken) is not IInvocationOperation invocationOperation)
                return false;

            // Find where the variable comes from
            var origin = ExpressionSyntaxHelper.TryExpand(lambdaExpression, semanticModel, cancellationToken);
            while (origin is not SyntaxExpressionOrigin)
            {
                switch (origin)
                {
                    case MemberExpressionOrigin parentMemberAccess:
                    {
                        origin = parentMemberAccess.Receiver;
                        break;
                    }
                    case ForwardingExpressionOrigin forwarding:
                    {
                        origin = forwarding.Receiver;
                        continue;
                    }
                    default:
                    {
                        break;
                    }
                }
            }
            if (origin is not SyntaxExpressionOrigin expressionRoot)
                return false;

            var argumentSyntax = expressionRoot.Syntax.FirstAncestorOrSelf<ArgumentSyntax>();
            if (argumentSyntax == null)
                return false; // Not from a function argument?          

            if (semanticModel.GetOperation(argumentSyntax, cancellationToken) is not IArgumentOperation argumentOperation)
                return false;
            var argument = invocationOperation.Arguments.FirstOrDefault(x => x == argumentOperation);
            if (argument == null)
                return false; // Not from this invocation?

            // Only Expression<delegate>
            if (argument.Value.Type == null ||
                !argument.Value.Type.HasName("Expression", "System.Linq.Expressions"))
            {
                return false;
            }

            //Get lambda parameters
            var lambdaParameters = argument.Value.Syntax switch
            {
                SimpleLambdaExpressionSyntax s => [semanticModel.GetDeclaredSymbol(s.Parameter, cancellationToken)],
                ParenthesizedLambdaExpressionSyntax p => p.ParameterList.Parameters
                    .Select(parameter => semanticModel.GetDeclaredSymbol(parameter, cancellationToken))
                    .ToArray(),
                _ => []
            };
            if (lambdaParameters.Length == 0)
                return false;

            // Check if it matches a lambda parameter
            var expressionRootSymbol = semanticModel.GetSymbolInfo(expressionRoot.Syntax, cancellationToken).Symbol;
            if (lambdaParameters.Any(x => SymbolEqualityComparer.Default.Equals(x, expressionRootSymbol)))
                return true; // Found
            return false;
        }
    }
}
