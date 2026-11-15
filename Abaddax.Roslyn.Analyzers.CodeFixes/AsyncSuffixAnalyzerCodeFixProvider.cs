using Abaddax.Roslyn.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using System.Collections.Immutable;
using System.Composition;

namespace Abaddax.Utilities.Analyzers.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncSuffixAnalyzerCodeFixProvider)), Shared]
    public sealed class AsyncSuffixAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
            = ImmutableArray.Create(AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find method to fix
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
                .ConfigureAwait(false);
            if (root == null)
                return;
            var methodDecl = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (methodDecl == null)
                return;
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken)
               .ConfigureAwait(false);
            if (semanticModel == null)
                return;
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl, context.CancellationToken);
            if (methodSymbol == null)
                return;

            string title = "Add 'Async' suffix to method name";

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedSolution: ct => AddAsyncSuffixAsync(context.Document, methodSymbol, ct),
                    equivalenceKey: title),
                diagnostic);
        }

        private static async Task<Solution> AddAsyncSuffixAsync(Document document, IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {
            var solution = document.Project.Solution;

            // Generate replacement
            var newName = methodSymbol.Name + "Async";

            // Produce a new solution and fix method name
            solution = await Renamer.RenameSymbolAsync(solution,
                methodSymbol,
                new SymbolRenameOptions(RenameOverloads: true, RenameInStrings: true, RenameInComments: true),
                newName,
                cancellationToken)
                .ConfigureAwait(false);

            return solution;
        }
    }
}
