using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.Analyzers
{
    public sealed class UncoditionalSelfRecursionAnalyzerTests
        : AnalyzerTestBase<UncoditionalSelfRecursionAnalyzer>
    {
        [Test]
        public async Task ShouldReportIfSimpleSelfRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void {|#0:Func|}()
                        {
                            Func();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldReportIfAsyncSelfRecursion()
        {
            var source =
                """
                using System.Threading.Tasks;

                namespace TestNamespace
                {
                    public class Test
                    {
                        public async Task {|#0:FuncAsync|}()
                        {
                            await FuncAsync();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldNotReportIfNoSelfRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            Func2();
                        }
                        public void Func2()
                        {
                            return;
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldNotReportIfConditionalRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func(int x)
                        {
                            if(x > 1)
                                Func(x - 1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldNotReportIfConditionalReturn()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func(int x)
                        {
                            if(x > 1)
                                return;
                            Func(x - 1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldReportIfStatementsBeforeOrAfterRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void {|#0:Func|}()
                        {
                            System.Console.WriteLine("Bla");
                            Func();
                            System.Console.WriteLine("Blub");
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldReportIfBranchButAllRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void {|#0:Func|}(int x)
                        {
                            if(x > 0)
                                Func(x - 1);
                            else
                                Func(x + 1);
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldReportIfBranchAfterRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void {|#0:Func|}(int x)
                        {
                            Func(x - 1);
                            if(x > 0)
                                return;
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldNotReportIfBaseCallRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class TestBase
                    {
                        public virtual void Func()
                        {
                            return;
                        }
                    }
                    public class Test : TestBase
                    {
                        public override void Func()
                        {
                            base.Func();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldReportIfThisCallRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class TestBase
                    {
                        public virtual void Func()
                        {
                            return;
                        }
                    }
                    public class Test : TestBase
                    {
                        public override void {|#0:Func|}()
                        {
                            this.Func();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(0)
                );
        }

        [Test]
        public async Task ShouldReportIfSimplePropertySelfRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public int Prop
                        {
                            {|#0:get|}
                            {
                                return Prop;
                            }
                            {|#1:set|}
                            {
                                Prop = value;
                            }
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(0),
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(1)
                );
        }
        [Test]
        public async Task ShouldNotReportIfConditionalPropertyRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        private bool _x;
                        public int Prop
                        {
                            get
                            {
                                if(_x)
                                    return 0;
                                return Prop;
                            }
                            set
                            {
                                if(!_x)
                                    Prop = value;
                            }
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldNotReportIfBasePropertyRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class TestBase
                    {
                        public virtual int Prop { get; set; }
                    }
                    public class Test : TestBase
                    {
                        public override int Prop
                        {
                            get
                            {
                                return base.Prop;
                            }
                            set
                            {
                                base.Prop = value;
                            }
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldReportIfThisPropertyRecursion()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class TestBase
                    {
                        public virtual int Prop { get; set; }
                    }
                    public class Test : TestBase
                    {
                        public override int Prop
                        {
                            {|#0:get|}
                            {
                                return this.Prop;
                            }
                            {|#1:set|}
                            {
                                this.Prop = value;
                            }
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(0),
                new DiagnosticResult(AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer, DiagnosticSeverity.Error)
                    .WithLocation(1)
                );
        }

    }


}
