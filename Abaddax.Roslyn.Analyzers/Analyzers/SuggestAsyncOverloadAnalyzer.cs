using Abaddax.Roslyn.Analyzers.Extensions;
using Abaddax.Roslyn.Analyzers.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SuggestAsyncOverloadAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor _Rule = new DiagnosticDescriptor(
            id: AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer,
            title: "Use async overload method",
            messageFormat: "Consider using '{0}Async' instead of '{0}' inside async functions",
            category: "AsyncUsage",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(_Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            var methodBlock = invocation.GetContainingMethodDeclarationBlock();
            if (methodBlock == null)
                return;
            if (context.SemanticModel.GetDeclaredSymbol(methodBlock, context.CancellationToken) is not IMethodSymbol callerMethod)
                return;
            // Skip inside sync methods
            if (!callerMethod.IsTaskedMethodDeclaration())
                return;

            var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
            if (symbol is not IMethodSymbol method)
                return;

            // Skip methods that already end with Async
            if (method.Name.EndsWith("Async", StringComparison.Ordinal))
                return;
            if (method.IsAsyncMethod())
                return;

            var receiverType = method.ReceiverType;
            // For member access (e.g. x.Func()), use the actual type of 'x' instead of ReceiverType.
            // ReceiverType may resolve to a base type or extension method target,
            // which may cause missing alternatives. So we use the actual type of the given variable instead
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken);
                receiverType = typeInfo.Type;
            }
            if (receiverType == null)
                return;

            //Check type
            if (!HasAsyncAlternative(callerMethod, method, receiverType, context.SemanticModel, invocation.Expression.SpanStart))
                return;

            var diagnostic = Diagnostic.Create(_Rule, invocation.GetLocation(), method.Name);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool HasAsyncAlternative(IMethodSymbol callerMethod, IMethodSymbol method, ITypeSymbol receiverType, SemanticModel semanticModel, int position)
        {
            var candidates = PotentialAsyncAlternatives(method, receiverType, semanticModel, position)
                // Do not suggest original method! Otherwise this could unintentianally cause a stack overflow 
                .Where(candidate => !SymbolEqualityComparer.Default.Equals(candidate, callerMethod))
                // Do not suggest obsolete methods
                .Where(candidate => !candidate.GetAttributes().Any(attr => attr.AttributeClass?.HasName("ObsoleteAttribute", "System") ?? false))
                // Validate compatible return
                .Where(candidate => candidate.IsAsyncCompatibleReturnType())
                .Where(candidate => HasCompatibleReturnType(candidate, method, semanticModel, position))
                // Validate compatible arguments
                .Where(candidate => HasCompatibleParameters(candidate, method, semanticModel, position));

            if (candidates.Any())
                return true;
            return false;
        }

        private static IEnumerable<IMethodSymbol> PotentialAsyncAlternatives(IMethodSymbol method, ITypeSymbol receiverType, SemanticModel semanticModel, int position)
        {
            var name = method.Name;
            var targetName = name + "Async";
            return MethodExtensions.ListPotentialAlternatives(receiverType, targetName, semanticModel, position)
                // Convert extension methods to reduced-form for correct parameter matching
                .Select(alternative =>
                {
                    if (!alternative.IsExtensionMethod)
                        return alternative;
                    var reducedAlternative = alternative.ReduceExtensionMethod(receiverType);
                    return reducedAlternative;
                })
                // Deduce generic parameters and construct generic methods
                .Select(alternative =>
                {
                    if (alternative == null)
                        return null;
                    if (!alternative.IsGenericMethod)
                        return alternative;
                    // Original type arguments
                    var genericArguments = method.TypeArguments;
                    // Check rought generic matches
                    if (genericArguments.Length != 0 && genericArguments.Length > alternative.TypeArguments.Length)
                        return null;
                    // Original was non generic, but alternative is generic
                    // -> deduce generic arguments
                    while (genericArguments.Length < alternative.TypeArguments.Length)
                    {
                        // Current type argument to deduce
                        var typeArgument = alternative.TypeArguments[genericArguments.Length];
                        // Already non generic?
                        if (typeArgument is not ITypeParameterSymbol)
                        {
                            genericArguments = genericArguments.Add(typeArgument);
                            continue;
                        }
                        var candidateTypes = alternative.Parameters
                            .Select((x, i) => (x.Type, Index: i))
                            .Where(x => SymbolEqualityComparer.Default.Equals(x.Type, typeArgument))
                            .Select(x => method.Parameters.ElementAtOrDefault(x.Index)?.Type)
                            .Where(x => x != null)
                            .Select(x => x!)
                            .ToList();
                        if (SymbolEqualityComparer.Default.Equals(alternative.ReturnType, typeArgument))
                            candidateTypes.Add(method.ReturnType);
                        // No match or ambigous match found
                        if (candidateTypes.Distinct(SymbolEqualityComparer.Default).ExactlyOneOrDefault() is not ITypeSymbol candidateType)
                            return null;
                        genericArguments = genericArguments.Add(candidateType);
                    }
                    return TypeCompatibilityHelper.ConstructGenericMethod(alternative.OriginalDefinition, genericArguments, genericArguments.Select(x => x.NullableAnnotation).ToImmutableArray(), semanticModel);
                })
                .Where(alternative => alternative != null)
                .Select(alternative => alternative!);
        }
        private static bool HasCompatibleReturnType(IMethodSymbol candidateMethod, IMethodSymbol baselineMethod, SemanticModel semanticModel, int position)
        {
            var candidateTaskReturn = candidateMethod.ReturnType.GetGenericParameter(0) ?? semanticModel.Compilation.GetSpecialType(SpecialType.System_Void);
            return TypeCompatibilityHelper.IsTypeAlternative(candidateTaskReturn, baselineMethod.ReturnType, semanticModel, position);
        }
        private static bool HasCompatibleParameters(IMethodSymbol candidateMethod, IMethodSymbol baselineMethod, SemanticModel semanticModel, int position)
        {
            if (baselineMethod.Parameters.Length > candidateMethod.Parameters.Length)
                return false;
            var length = Math.Max(baselineMethod.Parameters.Length, candidateMethod.Parameters.Length);
            if (length == 0)
                return true;
            var baselineParameters = baselineMethod.Parameters
                .Concat(Enumerable.Repeat((IParameterSymbol?)null, Math.Max(0, baselineMethod.Parameters.Length - length)))
                .Select((x, i) => (Index: i, BaselineParameter: x));
            var candidateParameters = candidateMethod.Parameters
                .Concat(Enumerable.Repeat((IParameterSymbol?)null, Math.Max(0, candidateMethod.Parameters.Length - length)))
                .Select((x, i) => (Index: i, CandidateParameter: x));
            var parameters = baselineParameters
                .Join(candidateParameters,
                    baseline => baseline.Index,
                    candidate => candidate.Index,
                    (baseline, candidate) => (baseline.Index, baseline.BaselineParameter, candidate.CandidateParameter))
                .OrderBy(x => x.Index)
                .Select(x => (x.BaselineParameter, x.CandidateParameter));

            // Check parameter compatibility
            var foundUnmatchedCancallationToken = false;
            foreach (var parameter in parameters)
            {
                if (parameter.CandidateParameter == null)
                    return false; // No match found
                if (foundUnmatchedCancallationToken)
                    return false; // Additional 'CancellationToken' is not at the end
                if (parameter.BaselineParameter == null)
                {
                    // Allow additional 'CancellationToken' at the end
                    if (parameter.CandidateParameter.Type.HasName("CancellationToken", "System.Threading"))
                    {
                        foundUnmatchedCancallationToken = true;
                        continue;
                    }
                    // Everything else -> no match -> fail
                    return false;
                }
                if (!TypeCompatibilityHelper.IsTypeAlternative(parameter.CandidateParameter.Type, parameter.BaselineParameter.Type, semanticModel, position))
                    return false;
            }
            return true;
        }
    }
}
