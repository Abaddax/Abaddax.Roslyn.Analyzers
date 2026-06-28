using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Test.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Test.Analyzers
{
    public sealed class AsyncSuffixAnalyzerTests
        : AnalyzerTestBase<AsyncSuffixAnalyzer>
    {
        [Test]
        public async Task ShouldReportIfTask()
        {
            var source =
                """
                using System.Threading.Tasks;

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
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0),
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(1)
                );
        }
        [Test]
        public async Task ShouldReportIfValueTask()
        {
            var source =
                """
                using System.Threading.Tasks;

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
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0),
                new DiagnosticResult(AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(1)
                );
        }
        [Test]
        public async Task ShouldNotReportIfAlreadyAsync()
        {
            var source =
                """
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public Task FuncAsync()
                        {
                            return Task.CompletedTask;
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
                using System.Threading.Tasks;

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
        [Test]
        public async Task ShouldNotReportIfControllerRoute()
        {
            var source =
                """
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;

                //Stub code
                namespace Microsoft.AspNetCore.Mvc
                {
                    public abstract class ControllerBase;
                }

                //Actual test
                namespace TestNamespace
                {
                    public class Api : ControllerBase
                    {
                        public Task Route()
                        {
                            return Task.CompletedTask;
                        }
                    }
                }
                """;

            await VerifyAnalyzerAsync(source, state =>
            {
                state.AnalyzerConfigFiles.Add(("/.editorconfig",
                    $"""
                    root = true

                    [*.cs]
                    dotnet_code_quality.{AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer}.ignore_controller = true
                    """));
            });
        }
        [Test]
        public async Task ShouldNotReportIfTestMethod()
        {
            var source =
                """
                using System;
                using System.Threading.Tasks;
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                using NUnit.Framework;
                using Xunit;

                //Stub code
                namespace Microsoft.VisualStudio.TestTools.UnitTesting
                {
                    public class TestMethodAttribute : Attribute;
                }
                namespace NUnit.Framework
                {
                    public class TestAttribute : Attribute;
                }
                namespace Xunit
                {
                    public class FactAttribute : Attribute;
                }

                //Actual test
                namespace TestNamespace
                {
                    public class Test
                    {
                        [TestMethod]
                        public Task MSTest()
                        {
                            return Task.CompletedTask;
                        }
                        [Test]
                        public Task NUnit()
                        {
                            return Task.CompletedTask;
                        }
                        [Fact]
                        public Task XUnit()
                        {
                            return Task.CompletedTask;
                        }
                    }
                }
                """;

            await VerifyAnalyzerAsync(source, state =>
            {
                state.AnalyzerConfigFiles.Add(("/.editorconfig",
                    $"""
                    root = true

                    [*.cs]
                    dotnet_code_quality.{AnalyzerIdentifiers.PreferAsyncSuffixAnalyzer}.ignore_tests = true
                    """));
            });
        }

    }
}
