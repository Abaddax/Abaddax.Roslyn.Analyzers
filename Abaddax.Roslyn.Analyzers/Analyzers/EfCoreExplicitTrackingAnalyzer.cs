using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EfCoreExplicitTrackingAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor _Rule = new DiagnosticDescriptor(
            id: AnalyzerIdentifiers.EfCoreExplicitTrackingAnalyzer,
            title: "Explicitly qualify the tracking behavior when using EF Core",
            messageFormat: "Consider explicitly specifying the tracking behavior for EF Core queries at the start",
            category: "Style",
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

            // Only consider simple member access: foo.Bar(...)
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return;

            // Only invocations directly on a DbSet<T> 
            var receiver = memberAccess.Expression;
            var receiverType = context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type;
            if (receiverType == null)
                return;
            if (!receiverType.IsDbSet())
                return;

            // Skip methods that already have specific tracking behaviour
            var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
            if (symbol is not IMethodSymbol method)
                return;
            if (method.HasName("AsTracking", "Microsoft.EntityFrameworkCore", "EntityFrameworkQueryableExtensions") ||
                method.HasName("AsNoTrackingWithIdentityResolution", "Microsoft.EntityFrameworkCore", "EntityFrameworkQueryableExtensions") ||
                method.HasName("AsNoTracking", "Microsoft.EntityFrameworkCore", "EntityFrameworkQueryableExtensions"))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(_Rule, receiver.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
