using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Test.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Test.Analyzers
{
    public sealed class CancellationTokenAnalyzerTests
        : AnalyzerTestBase<CancellationTokenAnalyzer>
    {
        protected override void SetupTestState(SolutionState state)
        {
            state.Sources.Add(
                """
                global using System.Threading;
                global using System.Threading.Tasks;
                """);

            base.SetupTestState(state);
        }

        [Test]
        public async Task ShouldReportIfTask()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task {|#0:Func|}()
                        {
                            return Task.CompletedTask;
                        }
                        public Task<int> {|#1:Func2|}()
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.AddCancellationTokenAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(0),
                new DiagnosticResult(AnalyzerIdentifiers.AddCancellationTokenAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(1)
                );
        }
        [Test]
        public async Task ShouldReportIfValueTask()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public ValueTask {|#0:Func|}()
                        {
                            return ValueTask.CompletedTask;
                        }
                        public ValueTask<int> {|#1:Func2|}()
                        {
                            return ValueTask.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.AddCancellationTokenAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(0),
                new DiagnosticResult(AnalyzerIdentifiers.AddCancellationTokenAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(1)
                );
        }
        [Test]
        public async Task ShouldNotReportIfAlreadyAdded()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task Func(CancellationToken cancellationToken)
                        {
                            return Task.CompletedTask;
                        }
                        public Task<int> Func2(CancellationToken cancellationToken, int x)
                        {
                            return Task.FromResult(1);
                        }
                        public ValueTask Func3(int x, CancellationToken cancellationToken)
                        {
                            return ValueTask.CompletedTask;
                        }
                        public ValueTask<int> Func4(CancellationToken cancellationToken)
                        {
                            return ValueTask.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldNotReportIfEntryPoint()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public static class Program
                    {
                        public static Task Main()
                        {
                            return Task.CompletedTask;
                        }
                    }
                }
                """;

            await VerifyAnalyzerAsync(source, OutputKind.ConsoleApplication);
        }


    }
}
