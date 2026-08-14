using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Supressors
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnusedCancallationTokenParameterSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor[] _UnusedParameters = new string[]
            {
                "IDE0060", // Remove unused parameter.
            }.Select(x => new SuppressionDescriptor(
                id: AnalyzerIdentifiers.UnusedCancallationTokenParameterSuppression,
                suppressedDiagnosticId: x,
                justification: "This is an exception variable inside a catch block. The warning is irellevant in this case."))
            .ToArray();

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; }
            = ImmutableArray.CreateRange(_UnusedParameters);

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
            // Find the node that triggered the warning
            var tree = diagnostic.Location.SourceTree;
            if (tree == null)
                return;

            var options = context.Options.GetGlobalOptions(tree);
            if (!options.IsEnabled(AnalyzerIdentifiers.UnusedCancallationTokenParameterSuppression, defaultValue: true))
                return;

            var root = tree.GetRoot(context.CancellationToken);
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is not ParameterSyntax { Parent: ParameterListSyntax { Parent: MethodDeclarationSyntax methodDecl } } parameterDecl)
                return;

            var semanticModel = context.GetSemanticModel(tree);

            var parameter = semanticModel.GetDeclaredSymbol(parameterDecl, context.CancellationToken);
            if (parameter == null)
                return;

            // Only for parameters of type CancellationToken
            if (!parameter.Type.HasName("CancellationToken", "System.Threading"))
                return;

            var method = semanticModel.GetDeclaredSymbol(methodDecl, context.CancellationToken);
            if (method == null)
                return;

            // Skip that are not async methods
            if (!method.IsTaskedMethodDeclaration())
                return;

            var descriptor = SupportedSuppressions
                .First(x => x.SuppressedDiagnosticId == diagnostic.Id);
            context.ReportSuppression(
                Suppression.Create(descriptor, diagnostic));
        }
    }
}
