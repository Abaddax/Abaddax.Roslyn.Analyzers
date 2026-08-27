using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.Analyzers
{
    public sealed class SuggestAsyncOverloadAnalyzerTests
        : AnalyzerTestBase<SuggestAsyncOverloadAnalyzer>
    {
        protected override void SetupTestState(SolutionState state)
        {
            state.Sources.Add(
                """
                global using System.Linq;
                global using System.Threading.Tasks;
                """);

            base.SetupTestState(state);
        }

        [Test]
        public async Task ShouldSuggestAsyncOverloadIfDirectCall()
        {
            var source =
                """
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            var x = {|#0:Func()|};
                            return Task.FromResult(x);
                        }
                        public int Func()
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync()
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfMemberCall()
        {
            var source =
                """
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            var x = {|#0:this.Func()|};
                            return Task.FromResult(x);
                        }
                        public int Func()
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync()
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfStaticCall()
        {
            var source =
                """
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public static class Test
                    {
                        public static Task<int> Main()
                        {
                            var x = {|#0:Func()|};
                            return Task.FromResult(x);
                        }
                        public static int Func()
                        {
                            return 1;
                        }
                        public static Task<int> FuncAsync()
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfExtensionCall()
        {
            var source =
                """
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            var x = {|#0:this.Func()|};
                            return Task.FromResult(x);
                        }
                    }
                    public static class Extension
                    {
                        
                        public static int Func(this Test test)
                        {
                            return 1;
                        }
                        public static Task<int> FuncAsync(this Test test)
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfExtensionCallInherited()
        {
            var source =
                """
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class TestBase;
                    public class Test : TestBase
                    {
                        public Task<int> Main()
                        {
                            var x = {|#0:this.Func()|};
                            var y = {|#1:this.Func2()|};
                            return Task.FromResult(x);
                        }
                    }
                    public static class Extension
                    {
                        
                        public static int Func(this TestBase test)
                        {
                            return 1;
                        }
                        public static int Func2(this Test test)
                        {
                            return 1;
                        }
                        public static Task<int> FuncAsync(this Test test)
                        {
                            return Task.FromResult(1);
                        }
                        public static Task<int> Func2Async(this TestBase test)
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0),
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(1)
                );
        }
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfExtensionCallDifferentExtension()
        {
            var source =
                """
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class TestBase;
                    public class Test : TestBase
                    {
                        public Task<int> Main()
                        {
                            var x = {|#0:this.Func()|};
                            return Task.FromResult(x);
                        }
                    }
                    public static class Extension1
                    {                        
                        public static int Func(this TestBase test)
                        {
                            return 1;
                        }
                    }
                    public static class Extension2
                    {
                        public static Task<int> FuncAsync(this Test test)
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfGenericExtensionCall()
        {
            var source =
                """
                using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test : List<int>
                    {
                        public Task<int[]> Main()
                        {
                            var x = {|#0:this.ToArray(1)|};
                            return Task.FromResult(x);
                        }
                        public int[] ToArray(int n)
                        {
                            return [n];
                        }
                    }
                    public static class Extension
                    {
                        public static Task<T[]> ToArrayAsync<T, T2>(this IEnumerable<T> test, T2 n)
                        {
                            return Task.FromResult(Array.Empty<T>());
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfTypeRoughlyMatch()
        {
            var source =
                """
                using System;                
                using System.Collections.Generic;
                using System.Linq;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            var x = {|#0:Func(default)|};
                            var y = {|#1:Func2(default)|};
                            return Task.FromResult(x);
                        }
                        public int Func(Span<byte> x)
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync(Memory<byte> x)
                        {
                            return Task.FromResult(1);
                        }
                        public int Func2(byte[] x)
                        {
                            return 1;
                        }
                        public Task<int> Func2Async(List<byte> x)
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0),
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(1)
                );
        }
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfAdditionalCancallationTokenParameter()
        {
            var source =
                """
                using System;                
                using System.Collections.Generic;
                using System.Linq;                
                using System.Threading;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            var x = {|#0:Func(default)|};
                            return Task.FromResult(x);
                        }
                        public int Func(string x)
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync(string x, CancellationToken cancalltionToken)
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldNotSuggestAsyncOverloadInSyncMethod()
        {
            var source =
                """
                using System;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public int Main()
                        {
                            var x = Func(1);
                            return x;
                        }
                        public int Func(int x)
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync(int x)
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldNotSuggestAsyncOverloadWithMissingParameters()
        {
            var source =
                """
                using System;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            var x = Func(default, 1);
                            return Task.FromResult(x);
                        }
                        public int Func(Span<byte> x, int y)
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync(Memory<byte> x)
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldNotSuggestAsyncOverloadWithImcompatibleParameters()
        {
            var source =
                """
                using System;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            var x = Func(default);
                            return Task.FromResult(x);
                        }
                        public int Func(Span<byte> x)
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync(string x)
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldNotSuggestAsyncOverloadIfSuggestionIsCurrentMethod()
        {
            var source =
                """
                using System;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public int Func(string x)
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync(string x)
                        {
                            var result = Func(x);
                            return Task.FromResult(result);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldSuggestAsyncOverloadIfDifferentButCompatibleGenericConstraints()
        {
            var source =
                """
                using System;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            var x = {|#0:Func<int>(1)|};
                            return Task.FromResult(x);
                        }
                        public int Func<TStruct>(TStruct x)
                            where TStruct : struct
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync<T>(T x)
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
              new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncOverloadAnalyzer, DiagnosticSeverity.Info)
                  .WithLocation(0)
              );
        }
        [Test]
        public async Task ShouldNotSuggestAsyncOverloadIfDifferentGenericConstraints()
        {
            var source =
                """
                using System;
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task<int> Main()
                        {
                            var x = Func<int>(1);
                            return Task.FromResult(x);
                        }
                        public int Func<T>(T x)
                            where T : struct
                        {
                            return 1;
                        }
                        public Task<int> FuncAsync<T>(T x)
                            where T : class
                        {
                            return Task.FromResult(1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }

    }
}
