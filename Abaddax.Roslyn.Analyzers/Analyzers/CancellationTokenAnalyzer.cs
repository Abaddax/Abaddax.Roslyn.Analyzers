using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CancellationTokenAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor _Rule = new DiagnosticDescriptor(
            id: AnalyzerIdentifiers.AddCancellationTokenAnalyzer,
            title: "Async method should accept CancellationToken",
            messageFormat: "Method '{0}' returns a Task/ValueTask and should accept a 'CancellationToken' parameter",
            category: "AsyncUsage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(_Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDecl = (MethodDeclarationSyntax)context.Node;

            var method = context.SemanticModel.GetDeclaredSymbol(methodDecl);
            if (method == null)
                return;

            // Skip methods that already have a CancellationToken
            if (method.Parameters.Any(p => p.Type.HasName("CancellationToken", "System.Threading")))
                return;
            if (!method.IsTaskedMethodDeclaration())
                return;

            // Edge cases to ignore
            if (method.IsEntryPoint(context))
                return;

            var diagnostic = Diagnostic.Create(_Rule, methodDecl.Identifier.GetLocation(), method.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
