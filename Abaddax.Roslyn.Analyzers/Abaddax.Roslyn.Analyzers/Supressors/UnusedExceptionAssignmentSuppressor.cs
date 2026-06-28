using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Supressors
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnusedExceptionAssignmentSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor[] _VariableAssignments = new string[]
            {
                "CS0168",  // The variable 'var' is declared but never used.
                "IDE0059", // Remove unnecessary value assignment.
            }.Select(x => new SuppressionDescriptor(
                id: AnalyzerIdentifiers.UnusedExceptionAssignmentSuppression,
                suppressedDiagnosticId: x,
                justification: "This is an exception variable inside a catch block. The warning is irellevant in this case."))
            .ToArray();

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; }
            = ImmutableArray.CreateRange(_VariableAssignments);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                if (!SupportedSuppressions.Any(x => x.SuppressedDiagnosticId == diagnostic.Id))
                    continue;

                // Find the node that triggered the warning
                var tree = diagnostic.Location.SourceTree;
                if (tree == null)
                    continue;

                var root = tree.GetRoot(context.CancellationToken);
                var node = root.FindNode(diagnostic.Location.SourceSpan);

                if (node is not CatchDeclarationSyntax)
                    continue;

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
