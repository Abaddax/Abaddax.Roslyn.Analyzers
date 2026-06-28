using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AsyncSuffixAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor _Rule = new DiagnosticDescriptor(
            id: AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer,
            title: "Async method should end with 'Async'",
            messageFormat: "Method '{0}' returns a Task/ValueTask and does not end with 'Async'",
            category: "AsyncUsage",
            defaultSeverity: DiagnosticSeverity.Info,
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

            var location = methodDecl.GetLocation();
            if (location?.SourceTree == null)
                return;

            var options = context.Options.GetGlobalOptions(location.SourceTree);

            var method = context.SemanticModel.GetDeclaredSymbol(methodDecl);
            if (method == null)
                return;

            // Skip sync methods
            if (!method.IsTaskedMethodDeclaration())
                return;
            // Skip methods that already end with Async
            if (method.Name.EndsWith("Async"))
                return;

            // Edge cases to ignore
            if (method.IsEntryPoint(context))
                return;
            if (IsControllerActionMethod(method, options))
                return;
            if (IsTestingMethod(method, options))
                return;

            var diagnostic = Diagnostic.Create(_Rule, methodDecl.Identifier.GetLocation(), method.Name);
            context.ReportDiagnostic(diagnostic);
        }
        private static bool IsControllerActionMethod(IMethodSymbol method, AnalyzerConfigOptions options)
        {
            //Only if option is enabled
            if (!options.IsSet(AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer, "ignore_controller"))
                return false;

            if (!method.IsInsideController())
                return false;
            if (method.DeclaredAccessibility != Accessibility.Public)
                return false;
            if (method.GetAttributes().Any(attr => attr.AttributeClass?.HasName("NonActionAttribute", "System.Web.Mvc") ?? false))
                return false;
            return true;
        }
        private static bool IsTestingMethod(IMethodSymbol method, AnalyzerConfigOptions options)
        {
            //Only if option is enabled
            if (!options.IsSet(AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer, "ignore_tests"))
                return false;

            if (method.GetAttributes()
                .Any(attr =>
                    //MSTest
                    (attr.AttributeClass?.HasName("TestMethodAttribute", "Microsoft.VisualStudio.TestTools.UnitTesting") ?? false) ||
                    //NUnit
                    (attr.AttributeClass?.HasName("TestAttribute", "NUnit.Framework") ?? false) ||
                    //XUnit
                    (attr.AttributeClass?.HasName("FactAttribute", "Xunit") ?? false)))
            {
                return true;
            }
            return false;

        }
    }
}
