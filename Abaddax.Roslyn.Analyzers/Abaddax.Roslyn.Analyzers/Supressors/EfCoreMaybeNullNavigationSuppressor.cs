using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Diagnostics;
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

                var root = tree.GetRoot(context.CancellationToken);
                var node = root.FindNode(diagnostic.Location.SourceSpan);

                if (!Debugger.IsAttached)
                    Debugger.Launch();

                if (node is ArgumentSyntax argumentSyntax)
                    node = argumentSyntax.Expression;
                if (node is not ExpressionSyntax expression)
                    continue;

                var semanticModel = context.GetSemanticModel(tree);
                var origin = ExpressionSyntaxHelper.TryExpand(expression, semanticModel, context.CancellationToken);
                if (origin == null)
                    continue;
#if DEBUG
                var x = origin.ToFullString();
#endif

                //Unwrap variable assignments
                if (node is IdentifierNameSyntax identifier)
                {
                    node = ExpressionSyntaxHelper.TraverseAssignments(identifier, semanticModel, context.CancellationToken);
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
                    //1. Check if from EF query                    
                    if (invocation.IsCalledFromDbContext(semanticModel, context.CancellationToken))
                    {
                        //2. Find Select
                        if (CheckQueryChainForSelect(invocation, semanticModel, context.CancellationToken))
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

        private static bool HasMaybeNullAttribute(IPropertySymbol propertySymbol)
        {
            return propertySymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.HasName("MaybeNullAttribute", "System.Diagnostics.CodeAnalysis") ?? false);
        }
        private static bool IsPropertyIncludedInQuery(
            ExpressionOrigin origin,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            List<string> propertyChain = new();
            // Go back to the root of the expression blogs.Posts.Entries -> blogs
            while (origin is MemberExpressionOrigin parentMemberAccess)
            {
                propertyChain.Add(parentMemberAccess.Member.Name);
                origin = parentMemberAccess.Receiver;
            }
            if (origin is AsyncSyntaxExpressionOrigin asyncOrigin)
                origin = asyncOrigin.Receiver;
            if (origin is not SyntaxExpressionOrigin expressionOrigin)
                return false;

            var queryExpression = expressionOrigin.Syntax;

            if (!queryExpression.IsCalledFromDbContext(semanticModel, cancellationToken))
                return false;

            // Analyze the EF Core Linq chain
            return CheckQueryChainForInclude(queryExpression, propertyChain.ToArray(), semanticModel, cancellationToken);
        }
        private static bool CheckQueryChainForInclude(
            ExpressionSyntax expression,
            string[] propertyChain,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (propertyChain.Length == 0)
                return false;

            // Walk down the method invocation chain (e.g., context.Blogs.Include(...).Where(...))
            var current = expression;
            var currentPropertyChain = propertyChain.ToList();
            while (current is InvocationExpressionSyntax invocation)
            {
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
                            if (!CheckSelectProperty(invocation))
                                return false; //Unsupported Select syntax -> abort
                        }
                    }

                    // Move up the chain
                    current = methodAccess.Expression;

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
                    bool CheckSelectProperty(InvocationExpressionSyntax invocation)
                    {
                        // Check if the argument points to our property (e.g., b => b.Posts)
                        var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
                        if (argument?.Expression is SimpleLambdaExpressionSyntax lambda &&
                            lambda.Body is ExpressionSyntax bodyExpression)
                        {
                            if (bodyExpression.IgnoreCasts().IgnoreNullSuppression() is MemberAccessExpressionSyntax lambdaMember)
                            {
                                // Selected one layer of the property path -> append to chain
                                var chain = propertyChain.ToList();
                                chain.Add(lambdaMember.Name.Identifier.Text);
                                propertyChain = chain.ToArray();
                                // Reset current chain
                                currentPropertyChain = chain;
                                return true;
                            }
                        }
                        return false;
                    }
                }
                else
                {
                    break;
                }
            }
            return false;
        }
        private static bool CheckQueryChainForSelect(
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
                        if (method.HasName("Select", "System.Linq", "Queryable"))
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
                            return true;
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
