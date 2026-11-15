using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Abaddax.Utilities.Analyzers.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.CodeFixes
{
    public sealed class CancellationTokenAnalyzerCodeFixProviderTests :
        CodeFixTestBase<CancellationTokenAnalyzerCodeFixProvider, CancellationTokenAnalyzer>
    {
        [Test]
        public async Task ShouldAddMissingCancellationTokenParameterWithoutExplicitNamespace()
        {
            var source = """
                using System.Threading.Tasks;
                using System.Threading;
                
                class Test
                {
                    public async Task {|#0:Func|}()
                    {
                        await Task.Delay(100);
                    }
                }
                """;
            var fixedSource = """
                using System.Threading.Tasks;
                using System.Threading;

                class Test
                {
                    public async Task Func(CancellationToken cancellationToken = default)
                    {
                        await Task.Delay(100);
                    }
                }
                """;
            await VerifyCodeFixAsync(source, fixedSource,
                new DiagnosticResult(AnalyzerIdentifiers.AddCancellationTokenAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(0));
        }
        [Test]
        public async Task ShouldAddMissingCancellationTokenParameterWithExplicitNamespace()
        {
            var source = """
                using System.Threading.Tasks;
                
                class Test
                {
                    public async Task {|#0:Func|}()
                    {
                        await Task.Delay(100);
                    }
                }
                """;
            var fixedSource = """
                using System.Threading.Tasks;

                class Test
                {
                    public async Task Func(System.Threading.CancellationToken cancellationToken = default)
                    {
                        await Task.Delay(100);
                    }
                }
                """;
            await VerifyCodeFixAsync(source, fixedSource,
                new DiagnosticResult(AnalyzerIdentifiers.AddCancellationTokenAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(0));
        }
        [Test]
        public async Task ShouldAddMissingCancellationTokenParameterInOverriddenMethods()
        {
            var source = """
                using System.Threading.Tasks;
                using System.Threading;
                
                class Test
                {
                    public virtual async Task {|#0:Func|}()
                    {
                        await Task.Delay(100);
                    }
                }
                class Derived : Test
                {
                    public override async Task Func()
                    {
                        await Task.Delay(200);
                    }
                }
                """;
            var fixedSource = """
                using System.Threading.Tasks;
                using System.Threading;

                class Test
                {
                    public virtual async Task Func(CancellationToken cancellationToken = default)
                    {
                        await Task.Delay(100);
                    }
                }
                class Derived : Test
                {
                    public override async Task Func(CancellationToken cancellationToken = default)
                    {
                        await Task.Delay(200);
                    }
                }
                """;
            await VerifyCodeFixAsync(source, fixedSource,
                new DiagnosticResult(AnalyzerIdentifiers.AddCancellationTokenAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(0));
        }
    }
}
