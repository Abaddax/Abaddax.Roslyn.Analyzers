using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Supressors
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ProtectedReadonlyFieldSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor[] _Suppressions = new string[]
            {
                "CA1051", // Do not declare visible instance fields.
            }.Select(x => new SuppressionDescriptor(
                id: AnalyzerIdentifiers.ProtectedReadonlyFieldSuppression,
                suppressedDiagnosticId: x,
                justification: "Declaring protected readonly fields is fine in some cases."))
            .ToArray();

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; }
            = ImmutableArray.CreateRange(_Suppressions);

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
            if (!options.IsEnabled(AnalyzerIdentifiers.ProtectedReadonlyFieldSuppression, defaultValue: false))
                return;

            var root = tree.GetRoot(context.CancellationToken);
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is not VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax fieldDecl } })
                return;

            if (fieldDecl.Modifiers.Any(x => x.IsKind(SyntaxKind.ProtectedKeyword)) &&
                fieldDecl.Modifiers.Any(x => x.IsKind(SyntaxKind.ReadOnlyKeyword)))
            {
                // Condition met! Suppress the warning.
                var descriptor = SupportedSuppressions
                    .First(x => x.SuppressedDiagnosticId == diagnostic.Id);
                context.ReportSuppression(
                    Suppression.Create(descriptor, diagnostic));
            }
        }
    }
}
