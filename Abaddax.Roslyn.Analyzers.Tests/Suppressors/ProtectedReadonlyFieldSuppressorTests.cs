using Abaddax.Roslyn.Analyzers.Suppressors;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.Suppressors
{
    public sealed class ProtectedReadonlyFieldSuppressorTests
        : SuppressorTestBase<ProtectedReadonlyFieldSuppressor>
    {
        protected override IEnumerable<DiagnosticAnalyzer> AdditionalAnalyzers
        {
            get
            {
                // Create CA1051 analyzer
                var type = Type.GetType("Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDeclareVisibleInstanceFieldsAnalyzer, Microsoft.CodeAnalysis.NetAnalyzers",
                    throwOnError: true)!;
                var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type)!;
                yield return analyzer;
            }
        }
        protected override void SetupTestState(SolutionState state)
        {
            state.AnalyzerConfigFiles.Add(("/.editorconfig",
                   $"""
                    root = true

                    [*.cs]
                    dotnet_code_quality.{AnalyzerIdentifiers.ProtectedReadonlyFieldSuppression}.enabled = true

                    [*.cs]
                    dotnet_diagnostic.CA1051.severity = warning
                    """));
            base.SetupTestState(state);
        }

        [Test]
        public async Task ShouldSuppressIfProtectedReadonlyField()
        {
            var source =
                """
                #pragma warning disable CS1591
                #pragma warning disable CS1998

                namespace TestNamespace
                {
                    public class Test
                    {
                        protected readonly int {|#0:_field|} = 1;
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CA1051")
                    .WithLocation(0)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfProtectedField()
        {
            var source =
                """
                #pragma warning disable CS1591
                #pragma warning disable CS1998

                namespace TestNamespace
                {
                    public class Test
                    {
                        protected int {|#0:_field|} = 1;
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CA1051")
                    .WithLocation(0)
                    .WithIsSuppressed(false)
                );
        }
    }
}
