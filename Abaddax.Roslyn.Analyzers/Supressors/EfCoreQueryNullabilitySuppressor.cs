using Abaddax.Roslyn.Analyzers.Extensions;
using Abaddax.Roslyn.Analyzers.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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

                // Find the node that triggered the nullability warning
                var tree = diagnostic.Location.SourceTree;
                if (tree == null)
                    continue;

                var options = context.Options.GetGlobalOptions(tree);
                if (!options.IsEnabled(AnalyzerIdentifiers.EfCoreQueryNullReferenceSuppression, defaultValue: false))
                    continue;

                var root = tree.GetRoot(context.CancellationToken);
                var node = root.FindNode(diagnostic.Location.SourceSpan);
                if (node is not ExpressionSyntax expression)
                    return;

                var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                if (invocation == null)
                    continue;

                var semanticModel = context.GetSemanticModel(tree);

                if (IsInsideEfCoreQuery(invocation, semanticModel, context.CancellationToken))
                {
                    if (IsQueryDelegateParameter(invocation, expression, semanticModel, context.CancellationToken))
                    {
                        var descriptor = SupportedSuppressions
                            .First(x => x.SuppressedDiagnosticId == diagnostic.Id);
                        if (descriptor != null)
                        {
                            context.ReportSuppression(
                                Suppression.Create(descriptor, diagnostic));
                        }
                    }
                }
            }
        }

        private static bool IsInsideEfCoreQuery(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (!invocation.IsCalledFromDbContext(semanticModel, cancellationToken))
                return false;

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
        private static bool IsQueryDelegateParameter(
            InvocationExpressionSyntax invocation,
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var argumentSyntax = expression.FirstAncestorOrSelf<ArgumentSyntax>();
            var argument = invocation.ArgumentList.Arguments
                .FirstOrDefault(x => ReferenceEquals(x, argumentSyntax));
            if (argument == null)
                return false; //Not from this invocation?
            if (argument.Expression is not LambdaExpressionSyntax lambda)
                return false;
            //Get lambda parameters
            var lambdaParameters = lambda switch
            {
                SimpleLambdaExpressionSyntax s => [semanticModel.GetDeclaredSymbol(s.Parameter, cancellationToken)],
                ParenthesizedLambdaExpressionSyntax p => p.ParameterList.Parameters
                    .Select(parameter => semanticModel.GetDeclaredSymbol(parameter, cancellationToken))
                    .ToArray(),
                _ => []
            };
            if (lambdaParameters.Length == 0)
                return false;

            //Find where the variable comes from
            var origin = ExpressionSyntaxHelper.TryExpand(expression, semanticModel, cancellationToken);
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

            //Check if it matches a lamda parameter
            var expressionRootSymbol = semanticModel.GetSymbolInfo(expressionRoot.Syntax, cancellationToken).Symbol;
            if (lambdaParameters.Any(x => SymbolEqualityComparer.Default.Equals(x, expressionRootSymbol)))
                return true; //Found -> suppress
            return false;
        }
    }
}
