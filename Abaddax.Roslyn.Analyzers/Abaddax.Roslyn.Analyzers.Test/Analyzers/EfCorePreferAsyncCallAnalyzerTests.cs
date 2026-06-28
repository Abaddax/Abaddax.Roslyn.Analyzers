using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Test.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Test.Analyzers
{
    public sealed class EfCorePreferAsyncCallAnalyzerTests
         : AnalyzerTestBase<EfCorePreferAsyncCallAnalyzer>
    {
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfIQueryable()
        {
            var source =
                """
                using System.Linq;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            IQueryable<int> query = null!;
                            var x = {|#0:query.First()|};
                            return Task.FromResult(x);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.EfCorePreferAsyncCallAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldNotSuggestAsyncOverloadInSyncMethod()
        {
            var source =
                """
                using System.Linq;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public int Main()
                        {
                            IQueryable<int> query = null!;
                            var x = query.First();
                            return x;
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldNotSuggestAsyncOverloadIfNotQueryable()
        {
            var source =
                """
                using System.Linq;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            IQueryable<int> query = null!;
                            var query2 = query.AsEnumerable();
                            var x = query2.First();
                            return Task.FromResult(x);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
    }
}
