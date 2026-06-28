using Abaddax.Roslyn.Analyzers.Extensions;
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

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            var methodBlock = invocation.GetContainingMethodDeclarationBlock();
            if (methodBlock == null)
                return;
            if (context.SemanticModel.GetDeclaredSymbol(methodBlock) is not IMethodSymbol callerSymbol)
                return;
            // Skip inside sync methods
            if (!callerSymbol.IsTaskedMethodDeclaration())
                return;

            var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol;
            if (symbol is not IMethodSymbol method)
                return;

            // Skip methods that already end with Async
            if (method.Name.EndsWith("Async"))
                return;
            if (method.IsAsyncMethod())
                return;

            if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodType)
                return;

            var receiverType = method.ReceiverType;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
                receiverType = typeInfo.Type;
            }
            if (receiverType == null)
                return;

            //Check type
            if (!HasAsyncAlternative(methodType, receiverType, context.SemanticModel, invocation.Expression.SpanStart, context.Compilation))
                return;

            var diagnostic = Diagnostic.Create(_Rule, invocation.GetLocation(), method.Name);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool HasAsyncAlternative(IMethodSymbol method, ITypeSymbol receiverType, SemanticModel model, int position, Compilation compilation)
        {
            var candidates = PotentialAsyncAlternatives(method, receiverType, model, position)
                .Where(candidates => candidates.ReturnType.IsAsyncCompatibleReturnType())
                .Where(candidate => HasSupersetOfParameterTypes(candidate, method, model, position, compilation));
            if (candidates.Any())
                return true;
            return false;
        }

        private static IEnumerable<IMethodSymbol> PotentialAsyncAlternatives(IMethodSymbol method, ITypeSymbol receiverType, SemanticModel model, int position)
        {
            var name = method.Name;
            var targetName = name + "Async";
            return MethodExtensions.ListPotentialAlternatives(receiverType, targetName, model, position);
        }
        private static bool HasSupersetOfParameterTypes(IMethodSymbol candidateMethod, IMethodSymbol baselineMethod, SemanticModel model, int position, Compilation compilation)
        {
            if (baselineMethod.Parameters.Length > candidateMethod.Parameters.Length)
                return false;
            return baselineMethod.Parameters
                .All(baselineParameter
                    => candidateMethod.Parameters.Any(candidateParameter
                        => IsAsyncParameterAlternative(candidateParameter, baselineParameter, model, position, compilation)));
        }
        private static bool IsAsyncParameterAlternative(IParameterSymbol canditateParameter, IParameterSymbol baselineParameter, SemanticModel model, int position, Compilation compilation)
        {
            return IsAsyncParameterAlternative(canditateParameter.Type, baselineParameter.Type, model, position, compilation);
        }
        private static bool IsAsyncParameterAlternative(ITypeSymbol canditateType, ITypeSymbol baselineType, SemanticModel model, int position, Compilation compilation)
        {
            // Exact match
            if (SymbolEqualityComparer.Default.Equals(canditateType, baselineType))
                return true;
            // Convertable match
            if (compilation.ClassifyConversion(baselineType, canditateType).Exists ||
                compilation.ClassifyConversion(canditateType, baselineType).Exists)
            {
                return true;
            }
            // Is Array-like
            if (IsArrayLikeEquivalent(canditateType, baselineType))
            {
                return true;
            }
            // Has conversion via ToX()/AsX()
            if (HasConversionMethod(canditateType, baselineType, model, position, compilation))
            {
                return true;
            }
            // Has convertsion via property
            if (HasConversionProperty(canditateType, baselineType))
            {
                return true;
            }
            return false;
        }
        private static bool HasConversionMethod(ITypeSymbol canditateType, ITypeSymbol baselineType, SemanticModel model, int position, Compilation compilation)
        {
            var canditateTypeName = canditateType.Name;
            if (canditateType is IArrayTypeSymbol)
                canditateTypeName = "Array";
            var targetNames = canditateTypeName switch
            {
                //Do not consider ToString a valid conversion!
                "String" => new[] { "AsString" },
                _ => new[] { $"To{canditateTypeName}", $"As{canditateTypeName}" }
            };

            foreach (var targetName in targetNames)
            {
                if (MethodExtensions.ListPotentialAlternatives(baselineType, targetName, model, position)
                    .Any(x => IsAsyncParameterAlternative(x.ReturnType, canditateType, model, position, compilation)))
                {
                    return true;
                }
            }
            return false;
        }
        private static bool HasConversionProperty(ITypeSymbol canditateType, ITypeSymbol baselineType)
        {
            if (baselineType is not INamedTypeSymbol named)
                return false;

            return named.GetMembers()
                .OfType<IPropertySymbol>()
                .Any(p => SymbolEqualityComparer.Default.Equals(p.Type, canditateType));
        }
        private static bool IsArrayLikeEquivalent(ITypeSymbol canditateType, ITypeSymbol baselineType)
        {
            if (!IsArrayLikeType(canditateType))
                return false;
            if (!IsArrayLikeType(baselineType))
                return false;

            var canditateElementType = canditateType.GetGenericParameter(0);
            var baselineElementType = baselineType.GetGenericParameter(0);

            return
                canditateElementType is not null &&
                baselineElementType is not null &&
                SymbolEqualityComparer.Default.Equals(canditateElementType, baselineElementType);

            static bool IsArrayLikeType(ITypeSymbol type)
            {
                return
                    type.HasName("Array", "System") ||
                    type.HasName("Span", "System") ||
                    type.HasName("ReadOnlySpan", "System") ||
                    type.HasName("Memory", "System") ||
                    type.HasName("ReadOnlyMemory", "System") ||
                    type.HasName("ArraySegment", "System");
            }
        }
    }
}
