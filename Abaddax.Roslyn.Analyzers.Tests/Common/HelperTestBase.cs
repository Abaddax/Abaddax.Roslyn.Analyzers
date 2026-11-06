using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Tests.Common
{
    public abstract class HelperTestBase
    {
        private static MetadataReference[] GetSystemMetadataReferences()
        {
            var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            var trustedAssembliesPaths = trustedAssemblies?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

            var references = trustedAssembliesPaths
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToArray();

            return references;
        }


        protected virtual IEnumerable<MetadataReference> AdditionalReferences
        {
            get
            {
                yield break;
            }
        }

        public SyntaxNode Parse([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            out SemanticModel semanticModel,
            int index = 0)
        {
            return ParseMultiple(source,
                out semanticModel)[index];
        }
        public SyntaxNode[] ParseMultiple([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            out SemanticModel semanticModel)
        {
            TestFileMarkupParser.GetSpans(source,
                out var output,
                out ImmutableArray<TextSpan> spans);

            var tree = CSharpSyntaxTree.ParseText(output);

            var compilation = CSharpCompilation.Create(
                assemblyName: "Tests",
                syntaxTrees: [tree],
                references: GetSystemMetadataReferences().Concat(AdditionalReferences),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

            semanticModel = compilation.GetSemanticModel(tree);

            // Check for errors
            var diagnostics = semanticModel.GetDiagnostics();
            using (Assert.EnterMultipleScope())
            {
                foreach (var error in diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error))
                {
                    Assert.Fail(error.ToString());
                }
            }

            return spans.Select(span => tree.GetRoot().FindNode(span))
                .ToArray();
        }
    }
}
