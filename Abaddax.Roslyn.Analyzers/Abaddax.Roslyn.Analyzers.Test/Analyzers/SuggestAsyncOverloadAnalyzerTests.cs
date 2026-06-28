using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Test.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Test.Analyzers
{
    public sealed class SuggestAsyncOverloadAnalyzerTests
        : AnalyzerTestBase<SuggestAsyncOverloadAnalyzer>
    {
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

    }
}
