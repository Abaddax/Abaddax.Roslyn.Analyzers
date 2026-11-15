using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Abaddax.Utilities.Analyzers.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.CodeFixes
{
    public sealed class AsyncSuffixAnalyzerCodeFixProviderTests
        : CodeFixTestBase<AsyncSuffixAnalyzerCodeFixProvider, AsyncSuffixAnalyzer>
    {
        [Test]
        public async Task ShouldRenameFunctionWithAsyncSuffix()
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
                    public async Task FuncAsync()
                    {
                        await Task.Delay(100);
                    }
                }
                """;
            await VerifyCodeFixAsync(source, fixedSource,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0));
        }
        [Test]
        public async Task ShouldAddRenameFunctionWithAsyncSuffixInOverriddenMethods()
        {
            var source = """
                using System.Threading.Tasks;
                
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

                class Test
                {
                    public virtual async Task FuncAsync()
                    {
                        await Task.Delay(100);
                    }
                }
                class Derived : Test
                {
                    public override async Task FuncAsync()
                    {
                        await Task.Delay(200);
                    }
                }
                """;
            await VerifyCodeFixAsync(source, fixedSource,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0));
        }
    }
}
