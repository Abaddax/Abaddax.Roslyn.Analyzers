using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EfCoreThenIncludeFormattingAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor _Rule = new DiagnosticDescriptor(
            id: AnalyzerIdentifiers.EfCoreThenIncludeFormattingAnalyzer,
            title: "'ThenInclude' formatting",
            messageFormat: "Consider placing 'ThenInclude' in the same line or indented one level compared to the 'Include'",
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

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            var location = invocation.GetLocation();
            if (location?.SourceTree == null)
                return;

            var options = context.Options.GetGlobalOptions(location.SourceTree);

            // Only consider simple member access: foo.Bar(...)
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return;

            // Only ThenInclude
            var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
            if (symbol is not IMethodSymbol method)
                return;
            if (!method.HasName("ThenInclude", "Microsoft.EntityFrameworkCore", "EntityFrameworkQueryableExtensions"))
                return;

            // Get call before ThenInclude
            if (memberAccess.Expression is not InvocationExpressionSyntax parentInvocation ||
                parentInvocation.Expression is not MemberAccessExpressionSyntax parentMemberAccess)
            {
                return;
            }

            // Only if ThenInclude is directly after Include or ThenInclude
            var parentSymbol = context.SemanticModel.GetSymbolInfo(parentInvocation, context.CancellationToken).Symbol;
            if (parentSymbol is not IMethodSymbol parentMethod)
                return;
            if (!parentMethod.HasName("Include", "Microsoft.EntityFrameworkCore", "EntityFrameworkQueryableExtensions") &&
                !parentMethod.HasName("ThenInclude", "Microsoft.EntityFrameworkCore", "EntityFrameworkQueryableExtensions"))
            {
                return;
            }

            if (!options.TryGetValue("indent_size", out var indentSizeStr) ||
                !int.TryParse(indentSizeStr, out int indentSize))
            {
                indentSize = 4;
            }

            var thenIncludePosition = GetPosition(memberAccess, indentSize);
            var includePosition = GetPosition(parentMemberAccess, indentSize);

            // Already on the same line.
            if (thenIncludePosition.Line == includePosition.Line)
                return;
            // Deeper indented is also ok
            if (thenIncludePosition.IndentationLevel > includePosition.IndentationLevel)
                return;

            var diagnostic = Diagnostic.Create(_Rule, invocation.GetLocation(), method.Name);
            context.ReportDiagnostic(diagnostic);
        }
        private static (int Line, int IndentationLevel) GetPosition(SyntaxNode syntaxNode, int indentationSize)
        {
            var tree = syntaxNode.SyntaxTree;
            var text = tree.GetText();

            var line = text.Lines.GetLineFromPosition(syntaxNode.Span.End);
            var lineText = text.GetSubText(TextSpan.FromBounds(line.Start, syntaxNode.Span.End)).ToString();

            var indentation = 0;
            foreach (var character in lineText)
            {
                if (character == ' ')
                    indentation++;
                else if (character == '\t')
                    indentation += indentationSize;
                else
                    break;
            }
            return (line.LineNumber, indentation / indentationSize);
        }
    }
}
