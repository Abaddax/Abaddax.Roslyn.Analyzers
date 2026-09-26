using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SuppressionJustificationAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor _Rule = new DiagnosticDescriptor(
            id: AnalyzerIdentifiers.SuppressionJustificationAnalyzer,
            title: "Suppression requires a justification",
            messageFormat: "Suppression of warning '{0}' requires a justification",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: AnalyzerIdentifiers.GetAnalyzerHelpUri(AnalyzerIdentifiers.SuppressionJustificationAnalyzer));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(_Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);

            context.RegisterSyntaxTreeAction(AnalyzePragmas);
        }

        private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
        {
            var attribute = (AttributeSyntax)context.Node;

            if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol attributeCtor)
                return;

            if (!attributeCtor.ContainingType.HasName("SuppressMessageAttribute", "System.Diagnostics.CodeAnalysis"))
                return;

            var attributeArgs = attribute.ArgumentList?.Arguments
                .Select(x => context.SemanticModel.GetOperation(x, context.CancellationToken))
                .ToArray();
            var justification = attributeArgs.OfType<ISimpleAssignmentOperation>()
                .Where(x => x.Target is IPropertyReferenceOperation { Property.Name: nameof(SuppressMessageAttribute.Justification) })
                .Where(x => x.Value.ConstantValue.HasValue)
                .Select(x => x.Value.ConstantValue.Value as string)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(justification))
            {
                // Missing justification.
                var checkId = attributeArgs.OfType<IArgumentOperation>()
                    .Where(x => x.Parameter?.Name == "checkId")
                    .Where(x => x.Value.ConstantValue.HasValue)
                    .Select(x => x.Value.ConstantValue.Value as string)
                    .FirstOrDefault();
                if (checkId != null && checkId.Contains(":"))
                    checkId = checkId.Split(':')[0];
                if (string.IsNullOrWhiteSpace(checkId))
                    checkId = "-";
                var diagnostic = Diagnostic.Create(_Rule, attribute.GetLocation(), checkId);
                context.ReportDiagnostic(diagnostic);
            }
        }
        private static void AnalyzePragmas(SyntaxTreeAnalysisContext context)
        {
            var options = context.Options.GetGlobalOptions(context.Tree);

            if (!options.IsSet(AnalyzerIdentifiers.SuppressionJustificationAnalyzer, "include_pragmas", defaultValue: false))
                return;

            var root = context.Tree.GetRoot(context.CancellationToken);

            foreach (var trivia in root.DescendantTrivia())
            {
                if (trivia.GetStructure() is not PragmaWarningDirectiveTriviaSyntax pragma)
                    continue;

                //  Only "#pragma warning disable ...."
                if (!pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                    continue;

                // Do not report for global disables and for the disable of this analyzer
                var errorCodes = pragma.ErrorCodes.Select(x => x.ToFullString()).ToArray();
                if (errorCodes.Length == 0 || errorCodes.Contains(AnalyzerIdentifiers.SuppressionJustificationAnalyzer))
                    continue;

                // Check whether this pragma has a justification.
                var trailingTrivia = pragma.ErrorCodes.LastOrDefault()?.GetTrailingTrivia() ?? pragma.DisableOrRestoreKeyword.TrailingTrivia;

                var comment = trailingTrivia.FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia));
                var text = comment.ToFullString();
                var justification = text
                    .TrimStart()
                    .TrimStart('/')
                    .Trim();
                if (string.IsNullOrWhiteSpace(justification))
                {
                    // Missing justification.
                    var suppressedDiagnostics = string.Join(";", errorCodes);
                    var diagnostic = Diagnostic.Create(_Rule, pragma.GetLocation(), suppressedDiagnostics);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
