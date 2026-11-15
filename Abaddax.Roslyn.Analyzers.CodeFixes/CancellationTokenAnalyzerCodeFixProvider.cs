using Abaddax.Roslyn.Analyzers;
using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Collections.Immutable;
using System.Composition;

namespace Abaddax.Utilities.Analyzers.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CancellationTokenAnalyzerCodeFixProvider)), Shared]
    public sealed class CancellationTokenAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
            = ImmutableArray.Create(AnalyzerIdentifiers.AddCancellationTokenAnalyzer);

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

            var title = "Add 'CancellationToken' parameter";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedSolution: ct => AddCancellationTokenAsync(context.Document, methodSymbol, ct),
                    equivalenceKey: title),
                diagnostic);
        }

        private static async Task<Solution> AddCancellationTokenAsync(Document document, IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {
            var solution = document.Project.Solution;

            // Find all overrides
            var overrides = await SymbolFinder.FindOverridesAsync(
                methodSymbol,
                document.Project.Solution,
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var methodsToReplace = overrides.OfType<IMethodSymbol>().Prepend(methodSymbol);

            // Group by document
            var methodsToReplaceByDocument = new Dictionary<DocumentId, List<MethodDeclarationSyntax>>();
            foreach (var method in methodsToReplace)
            {
                foreach (var reference in method.DeclaringSyntaxReferences)
                {
                    var syntax = await reference.GetSyntaxAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (syntax is not MethodDeclarationSyntax methodDecl)
                        continue;

                    var currentDocument = solution.GetDocument(methodDecl.SyntaxTree);
                    if (currentDocument == null)
                        continue;
                    if (!methodsToReplaceByDocument.TryGetValue(currentDocument.Id, out var declarations))
                    {
                        declarations = new();
                        methodsToReplaceByDocument.Add(currentDocument.Id, declarations);
                    }
                    declarations.Add(methodDecl);
                }
            }
            // Add new parameter to all methods+
            foreach (var documentGroup in methodsToReplaceByDocument)
            {
                var currentDocument = solution.GetDocument(documentGroup.Key);
                if (currentDocument == null)
                    continue;

                var semanticModel = await currentDocument.GetSemanticModelAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (semanticModel == null)
                    return solution;

                var currentRoot = await currentDocument.GetSyntaxRootAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (currentRoot == null)
                    continue;

                var updatedRoot = currentRoot.ReplaceNodes(documentGroup.Value,
                    (methodDecl, _) =>
                    {
                        // Generate new parameter
                        var cancellationTokenType = semanticModel
                            .LookupNamespacesAndTypes(methodDecl.SpanStart)
                            .OfType<ITypeSymbol>()
                            .Any(x => x.HasName("CancellationToken", "System.Threading"))
                                ? "CancellationToken"
                                : "System.Threading.CancellationToken";
                        var cancellationTokenParameter = SyntaxFactory
                            .Parameter(SyntaxFactory.Identifier("cancellationToken"))
                            .WithType(SyntaxFactory.ParseTypeName(cancellationTokenType))
                            .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression)));

                        return methodDecl.AddParameterListParameters(cancellationTokenParameter);
                    });

                solution = solution.WithDocumentSyntaxRoot(
                    documentGroup.Key,
                    updatedRoot);
            }

            return solution;
        }
    }
}
