using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using static Abaddax.Roslyn.Analyzers.Extensions.ExpressionSyntaxHelper;

namespace Abaddax.Roslyn.Analyzers.Supressors
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EfCoreMaybeNullNavigationSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor[] _NullDereferences = new string[]
            {
                "CS8600", // Converting null literal or possible null value to non-nullable type.
                "CS8601", // Possible null reference assignment.
                "CS8602", // Dereference of a possibly null reference.
                "CS8603", // Possible null reference return.
                "CS8604", // Possible null reference argument.
                "CS8605", // Unboxing a possibly null value.
                "CS8607", // A possible null value may not be used for a type marked with [NotNull] or [DisallowNull]
                "CS8629", // Nullable value type may be null.
                "CS8714", // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint
            }.Select(x => new SuppressionDescriptor(
                id: AnalyzerIdentifiers.EfCoreDereferencePossibleNullReferenceSuppression,
                suppressedDiagnosticId: x,
                justification: "EF Core navigation property marked with [MaybeNull] is only null when not included."))
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
                if (!options.IsEnabled(AnalyzerIdentifiers.EfCoreDereferencePossibleNullReferenceSuppression, defaultValue: false))
                    continue;

                var root = tree.GetRoot(context.CancellationToken);
                var node = root.FindNode(diagnostic.Location.SourceSpan);

                if (node is ArgumentSyntax argumentSyntax)
                    node = argumentSyntax.Expression;
                if (node is not ExpressionSyntax)
                    continue;

                var semanticModel = context.GetSemanticModel(tree);

                //Unwrap variable assignments
                if (node is IdentifierNameSyntax identifier)
                {
                    node = TraverseAssignments(identifier, semanticModel, context.CancellationToken);
                }
                //Unwarp awaits
                if (node is AwaitExpressionSyntax asyncAccess)
                {
                    node = asyncAccess.Expression;
                }
                // Nested property access
                if (node is MemberAccessExpressionSyntax memberAccess)
                {
                    //1. Check if the property has the [MaybeNull] attribute
                    var symbol = semanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
                    if (symbol is IPropertySymbol propertySymbol &&
                        HasMaybeNullAttribute(propertySymbol))
                    {
                        var origin = TryExpand(memberAccess, semanticModel, context.CancellationToken);
                        if (origin == null)
                            continue;

                        //2. Trace the variable back to see if it was included
                        if (IsPropertyIncludedInQuery(origin, semanticModel, context.CancellationToken))
                        {
                            //Condition met! Suppress the warning.
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
                // Direct variable access e.g. when query.Select(x => x.Prop).A
                if (node is InvocationExpressionSyntax invocation)
                {
                    //1. Check if the select points to property with [MaybeNull] attribute
                    if (IsMaybeNullProperySelection(invocation, semanticModel, context.CancellationToken))
                    {
                        var origin = TryExpand(invocation, semanticModel, context.CancellationToken);
                        if (origin == null)
                            continue;

                        //1. Check if from EF query
                        _ = BuildPropertyChain(origin, semanticModel, context.CancellationToken, out var isCalledFromDbContext);
                        if (isCalledFromDbContext)
                        {
                            //2. Trace the variable back to see if it was included
                            if (IsPropertyIncludedInQuery(origin, semanticModel, context.CancellationToken))
                            {
                                //Condition met! Suppress the warning.
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
            }
        }

        /// <summary>
        /// Look for [MaybeNull] on property
        /// </summary>
        private static bool HasMaybeNullAttribute(IPropertySymbol propertySymbol)
        {
            return propertySymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.HasName("MaybeNullAttribute", "System.Diagnostics.CodeAnalysis") ?? false);
        }
        /// <summary>
        /// Check if the <paramref name="origin"/> path is included in the query
        /// </summary>
        private static bool IsPropertyIncludedInQuery(
            ExpressionOrigin origin,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var propertyChain = BuildPropertyChain(origin, semanticModel, cancellationToken,
                out var isCalledFromDbContext);
            if (!isCalledFromDbContext)
                return false;

            // Analyze the EF Core Linq chain
            return CheckQueryChainForInclude(origin, propertyChain.ToArray(), semanticModel, cancellationToken);
        }
        /// <summary>
        /// Build the property path referenced by <paramref name="origin"/>
        /// </summary>
        /// <returns>Propery-Chain. Items are in reverse! So x.A.B -> [B,A]</returns>
        private static string[] BuildPropertyChain(
            ExpressionOrigin origin,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            out bool isCalledFromDbContext)
        {
            List<string> propertyChain = new();
            // Go back to the root (DbSet) of the expression blogs.Posts.Entries -> blogs
            while (origin is not SyntaxExpressionOrigin)
            {
                switch (origin)
                {
                    case MemberExpressionOrigin parentMemberAccess:
                    {
                        if (parentMemberAccess.Member is IPropertySymbol property &&
                            !property.Type.IsDbSet())
                        {
                            propertyChain.Add(property.Name);
                        }
                        origin = parentMemberAccess.Receiver;
                        continue;
                    }
                    case InvocationSyntaxExpressionOrigin invocation
                        when invocation.Invocation.Expression is MemberAccessExpressionSyntax methodAccess:
                    {
                        var symbol = semanticModel
                            .GetSymbolInfo(methodAccess, cancellationToken)
                            .Symbol;

                        if (symbol is IMethodSymbol method)
                        {
                            if (method.HasName("Select", "System.Linq", "Queryable") ||
                                method.HasName("Select", "System.Linq", "Enumerable") ||
                                method.HasName("SelectMany", "System.Linq", "Queryable") ||
                                method.HasName("SelectMany", "System.Linq", "Enumerable"))
                            {
                                // Check if the argument points to our property (e.g., b => b.Posts)
                                var argument = invocation.Invocation.ArgumentList.Arguments.FirstOrDefault();
                                if (argument?.Expression is SimpleLambdaExpressionSyntax lambda &&
                                    lambda.Body is ExpressionSyntax bodyExpression)
                                {
                                    if (bodyExpression.IgnoreCasts().IgnoreNullSuppression() is MemberAccessExpressionSyntax lambdaMember)
                                    {
                                        var selectedMemberSymbol = semanticModel.GetSymbolInfo(lambdaMember, cancellationToken).Symbol;
                                        if (selectedMemberSymbol is IPropertySymbol selectedProperty)
                                        {
                                            propertyChain.Add(selectedProperty.Name);
                                        }
                                    }
                                }
                            }
                        }
                        origin = invocation.Receiver;
                        continue;
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

            if (origin is not SyntaxExpressionOrigin expressionRoot ||
               !expressionRoot.Syntax.IsCalledFromDbContext(semanticModel, cancellationToken))
            {
                isCalledFromDbContext = false;
                return [];
            }
            isCalledFromDbContext = true;
            return propertyChain.ToArray();
        }
        /// <summary>
        /// Check if the <paramref name="origin"/> in included in the query via Include/ThenInclude
        /// </summary>
        private static bool CheckQueryChainForInclude(
            ExpressionOrigin origin,
            string[] propertyChain,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (propertyChain.Length == 0)
                return false;

            // Walk down the method invocation chain (e.g., context.Blogs.Include(...).Where(...))
            var current = origin;
            var currentPropertyChain = propertyChain.ToList();
            while (current is not SyntaxExpressionOrigin and not null)
            {
                switch (current)
                {
                    case InvocationSyntaxExpressionOrigin invocationOrigin:
                    {
                        var invocation = invocationOrigin.Invocation;
                        if (invocation.Expression is MemberAccessExpressionSyntax methodAccess)
                        {
                            var symbol = semanticModel
                                .GetSymbolInfo(methodAccess, cancellationToken)
                                .Symbol;

                            if (symbol is IMethodSymbol method)
                            {
                                if (method.HasName("Include", "Microsoft.EntityFrameworkCore", "EntityFrameworkQueryableExtensions") &&
                                   currentPropertyChain.Count == 1)
                                {
                                    if (CheckIncludeProperty(invocation))
                                        return true;
                                    //Current Include.ThenInclude chain finished -> reset
                                    currentPropertyChain = propertyChain.ToList();
                                }
                                else if (method.HasName("ThenInclude", "Microsoft.EntityFrameworkCore", "EntityFrameworkQueryableExtensions") &&
                                    currentPropertyChain.Count > 1)
                                {
                                    if (CheckIncludeProperty(invocation))
                                        return true;
                                }
                                else if (method.HasName("Select", "System.Linq", "Queryable") || method.HasName("SelectMany", "System.Linq", "Queryable"))
                                {
                                    var result = CheckSelectProperty(invocation);
                                    if (result == null)
                                        return false; //Unsupported Select syntax -> abort
                                    if (result == true)
                                        return true;
                                }
                            }

                            // Move up the chain
                            current = invocationOrigin.Receiver;
                            break;

                            bool CheckIncludeProperty(InvocationExpressionSyntax invocation)
                            {
                                // Check if the argument points to our property (e.g., b => b.Posts)
                                var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
                                if (argument?.Expression is SimpleLambdaExpressionSyntax lambda &&
                                    lambda.Body is ExpressionSyntax bodyExpression)
                                {
                                    if (bodyExpression.IgnoreCasts().IgnoreNullSuppression() is MemberAccessExpressionSyntax lambdaMember)
                                    {
                                        // Found one layer of the property path
                                        if (currentPropertyChain[0] == lambdaMember.Name.Identifier.Text)
                                            currentPropertyChain.RemoveAt(0);
                                        // Found full property path
                                        if (currentPropertyChain.Count == 0)
                                            return true;
                                    }
                                }
                                return false;
                            }
                            bool? CheckSelectProperty(InvocationExpressionSyntax invocation)
                            {
                                // Check if the argument points to our property (e.g., b => b.Posts)
                                var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
                                if (argument?.Expression is SimpleLambdaExpressionSyntax lambda &&
                                    lambda.Body is ExpressionSyntax bodyExpression)
                                {
                                    if (bodyExpression.IgnoreCasts().IgnoreNullSuppression() is MemberAccessExpressionSyntax lambdaMember)
                                    {
                                        // Found one layer of the property path
                                        if (currentPropertyChain[0] == lambdaMember.Name.Identifier.Text)
                                            currentPropertyChain.RemoveAt(0);
                                        // Found full property path
                                        if (currentPropertyChain.Count == 0)
                                            return true;
                                        return false;
                                    }
                                }
                                return null;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    case MemberExpressionOrigin parentMemberAccess:
                    {
                        current = parentMemberAccess.Receiver;
                        break;
                    }
                    case ForwardingExpressionOrigin forwarding:
                    {
                        current = forwarding.Receiver;
                        continue;
                    }
                    default:
                    {
                        current = null;
                        break;
                    }
                }
            }
            return false;
        }
        private static bool IsMaybeNullProperySelection(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            // Walk down the method invocation chain (e.g., context.Blogs.Include(...).Where(...))
            var current = expression;
            while (current is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax methodAccess)
                {
                    var symbol = semanticModel
                        .GetSymbolInfo(methodAccess, cancellationToken)
                        .Symbol;

                    if (symbol is IMethodSymbol method)
                    {
                        if (method.HasName("Select", "System.Linq", "Queryable") ||
                            method.HasName("Select", "System.Linq", "Enumerable"))
                        {
                            // Check if the argument points to our property (e.g., b => b.Posts)
                            var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
                            if (argument?.Expression is SimpleLambdaExpressionSyntax lambda &&
                                lambda.Body is ExpressionSyntax bodyExpression)
                            {
                                if (bodyExpression.IgnoreCasts().IgnoreNullSuppression() is MemberAccessExpressionSyntax lambdaMember)
                                {
                                    var memberSymbol = semanticModel.GetSymbolInfo(lambdaMember, cancellationToken).Symbol;
                                    if (memberSymbol is IPropertySymbol propertySymbol && HasMaybeNullAttribute(propertySymbol))
                                        return true;
                                }
                            }
                            return false;
                        }
                    }

                    // Move up the chain
                    current = methodAccess.Expression;
                }
                else
                {
                    break;
                }
            }
            return false;
        }
    }
}
