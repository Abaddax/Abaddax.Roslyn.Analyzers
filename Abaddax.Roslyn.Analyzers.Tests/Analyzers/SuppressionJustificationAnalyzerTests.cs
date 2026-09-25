using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.Analyzers
{
    public sealed partial class SuppressionJustificationAnalyzerTests
        : AnalyzerTestBase<SuppressionJustificationAnalyzer>
    {
        protected override void SetupTestState(SolutionState state)
        {
            state.AnalyzerConfigFiles.Add(("/.editorconfig",
                   $"""
                    root = true

                    [*.cs]
                    dotnet_code_quality.{AnalyzerIdentifiers.SuppressionJustificationAnalyzer}.include_pragmas = true
                    """));
            base.SetupTestState(state);
        }

        [Test]
        public async Task ShouldReportIfMissingJustificationPragma()
        {
            var source =
                """
                namespace TestNamespace
                {
                {|#0:#pragma warning disable ABC123|}
                    public class Test;
                }
                """;

            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.SuppressionJustificationAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("ABC123")
                );
        }
        [Test]
        public async Task ShouldNotReportIfExistingJustificationPragma()
        {
            var source =
                """
                namespace TestNamespace
                {
                #pragma warning disable ABC123 // Because this is a test
                #pragma warning disable
                    public class Test;
                }
                """;

            await VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task ShouldReportIfMissingJustificationAttribute()
        {
            var source =
                """
                using System.Diagnostics.CodeAnalysis;

                [assembly: {|#0:SuppressMessage("Test", "ABC123:Test123")|}]
                [module: {|#1:SuppressMessage("Test", "ABC123")|}]
                namespace TestNamespace
                {
                    [{|#2:SuppressMessage("Test", "ABC123")|}]
                    public class Test<[{|#3:SuppressMessage("Test", "ABC123")|}] T>
                    {
                        [return: {|#4:SuppressMessage("Test", "ABC123")|}]
                        [{|#5:SuppressMessage("Test", "ABC123")|}]
                        public void Func([{|#6:SuppressMessage("Test", "ABC123")|}] int i)
                        {

                        }
                    }
                }
                """;

            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.SuppressionJustificationAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("ABC123"),
                new DiagnosticResult(AnalyzerIdentifiers.SuppressionJustificationAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(1)
                    .WithArguments("ABC123"),
                 new DiagnosticResult(AnalyzerIdentifiers.SuppressionJustificationAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(2)
                    .WithArguments("ABC123"),
                new DiagnosticResult(AnalyzerIdentifiers.SuppressionJustificationAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(3)
                    .WithArguments("ABC123"),
                new DiagnosticResult(AnalyzerIdentifiers.SuppressionJustificationAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(4)
                    .WithArguments("ABC123"),
                new DiagnosticResult(AnalyzerIdentifiers.SuppressionJustificationAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(5)
                    .WithArguments("ABC123"),
                new DiagnosticResult(AnalyzerIdentifiers.SuppressionJustificationAnalyzer, DiagnosticSeverity.Warning)
                    .WithLocation(6)
                    .WithArguments("ABC123")
            );
        }
        [Test]
        public async Task ShouldNotReportIfExistingJustificationAttribute()
        {
            var source =
                """
                using System.Diagnostics.CodeAnalysis;
                
                [assembly: SuppressMessage("Test", "ABC123:Test123", Justification = "Because this is a test")]
                [module: SuppressMessage("Test", "ABC123", Justification = "Because this is a test")]
                namespace TestNamespace
                {
                    [SuppressMessage("Test", "ABC123", Justification = "Because this is a test")]
                    public class Test<[SuppressMessage("Test", "ABC123", Justification = "Because this is a test")] T>
                    {
                        [return: SuppressMessage("Test", "ABC123", Justification = "Because this is a test")]
                        [SuppressMessage("Test", "ABC123", Justification = "Because this is a test")]
                        public void Func([SuppressMessage("Test", "ABC123", Justification = "Because this is a test")] int i)
                        {
                
                        }
                    }
                }
                """;

            await VerifyAnalyzerAsync(source);
        }

    }
}
