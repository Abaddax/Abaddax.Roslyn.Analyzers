using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Reflection;

namespace Abaddax.Roslyn.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EfCorePreferAsyncCallAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor _Rule = new DiagnosticDescriptor(
            id: AnalyzerIdentifiers.EfCorePreferAsyncCallAnalyzer,
            title: "Use async EF Core methods",
            messageFormat: "Consider using '{0}Async' instead of '{0}' for EF Core queries inside async methods",
            category: "AsyncUsage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly IReadOnlyCollection<string> _EfSyncMethods = new HashSet<string>()
        {
            "All",
            "Any",
            "Average",
            "Contains",
            "Count",
            "First",
            "FirstOrDefault",
            "ForEach",
            "LongCount",
            "Max",
            "Min",
            "Single",
            "SingleOrDefault",
            "Sum",
            "ToArray",
            "ToDictionary",
            "ToList"
        };

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

            // Only consider simple member access: foo.Bar(...)
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return;

            var methodBlock = invocation.GetCurrentMethodDeclarationBlock();
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
            // Skip non EF methods
            if (!_EfSyncMethods.Contains(method.Name))
                return;

            var receiver = memberAccess.Expression;
            var receiverType = context.SemanticModel.GetTypeInfo(receiver).Type;
            if (receiverType == null)
                return;

            if (!ImplementsIQueryable(receiverType))
                return;

            var diagnostic = Diagnostic.Create(_Rule, invocation.GetLocation(), method.Name);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool ImplementsIQueryable(ITypeSymbol type)
        {
            if (type.HasName("IQueryable", "System.Linq"))
                return true;
            foreach (var interfaceType in type.AllInterfaces)
            {
                if (ImplementsIQueryable(interfaceType))
                    return true;
            }
            return false;
        }
    }
}
