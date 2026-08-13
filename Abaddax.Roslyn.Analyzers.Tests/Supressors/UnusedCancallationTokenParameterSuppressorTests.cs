using Abaddax.Roslyn.Analyzers.Supressors;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.Supressors
{
    public sealed class UnusedCancallationTokenParameterSuppressorTests
        : SuppressorTestBase<UnusedCancallationTokenParameterSuppressor>
    {
        protected override IEnumerable<DiagnosticAnalyzer> AdditionalAnalyzers
        {
            get
            {
                //Create IDE0060 analyzer
                var type = Type.GetType("Microsoft.CodeAnalysis.CSharp.RemoveUnusedParametersAndValues.CSharpRemoveUnusedParametersAndValuesDiagnosticAnalyzer, Microsoft.CodeAnalysis.CSharp.Features",
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
                    dotnet_diagnostic.IDE0060.severity = warning
                    dotnet_code_quality_unused_parameters = all:warning                    
                    """));
            base.SetupTestState(state);
        }

        [Test]
        public async Task ShouldSuppressIfInsideAsyncFunction()
        {
            var source =
                """
                using System.Threading;
                using System.Threading.Tasks;

                #pragma warning disable CS1591
                #pragma warning disable CS1998

                namespace TestNamespace
                {
                    public class Test
                    {
                        public async Task FuncAsync(CancellationToken {|#0:cancallationToken|})
                        {
                            return;
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("IDE0060")
                    .WithLocation(0)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfInsideSyncFunction()
        {
            var source =
                """
                using System.Threading;

                #pragma warning disable CS1591
                #pragma warning disable CS1998

                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func(CancellationToken {|#0:cancallationToken|})
                        {
                            return;
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("IDE0060")
                    .WithLocation(0)
                    .WithIsSuppressed(false)
                );
        }

    }
}
