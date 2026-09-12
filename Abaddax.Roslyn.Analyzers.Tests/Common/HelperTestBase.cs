using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
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

        public void Parse([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            out SemanticModel semanticModel,
            out SyntaxNode syntaxNode,
            int index = 0)
        {
            TestFileMarkupParser.GetSpan(source, out var output, out var span);

            var tree = CSharpSyntaxTree.ParseText(output);

            var compilation = CSharpCompilation.Create(
                assemblyName: "Tests",
                syntaxTrees: [tree],
                references: GetSystemMetadataReferences().Concat(AdditionalReferences),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

            semanticModel = compilation.GetSemanticModel(tree);
            syntaxNode = tree.GetRoot().FindNode(span);

            var diagnostics = semanticModel.GetDiagnostics();
            using (Assert.EnterMultipleScope())
            {
                foreach (var error in diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error))
                {
                    Assert.Fail(error.ToString());
                }
            }
        }
    }
}
