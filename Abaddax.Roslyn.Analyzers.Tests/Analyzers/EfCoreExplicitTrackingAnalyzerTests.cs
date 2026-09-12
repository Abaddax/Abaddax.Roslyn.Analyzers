using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.Analyzers
{
    public sealed class EfCoreExplicitTrackingAnalyzerTests
        : AnalyzerTestBase<EfCoreExplicitTrackingAnalyzer>
    {
        protected override void SetupTestState(SolutionState state)
        {
            state.Sources.Add("""
                global using System;
                global using System.Collections;
                global using System.Collections.Generic;
                global using System.Diagnostics.CodeAnalysis;
                global using System.Linq;
                global using System.Linq.Expressions;
                global using System.Threading;
                global using System.Threading.Tasks;
                global using Microsoft.EntityFrameworkCore;
                global using Microsoft.EntityFrameworkCore.Query;
                """);
            state.Sources.Add(
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class TestContext : DbContext
                    {
                        public class TestEntity
                        {
                            [MaybeNull]
                            public TestEntity Mother { get; set; }
                            [MaybeNull]
                            public TestEntity Father { get; set; }
                            public List<TestEntity> Childs { get; set; } = new();
                        }
                        public DbSet<TestEntity> Persons { get; set; } = null!;
                    }
                }
                """);
            state.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location));
            base.SetupTestState(state);
        }

        [Test]
        public async Task ShouldSuggestMissingTrackingBehaviour()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            var db = new TestContext();
                
                            var p = {|#0:db.Persons|}
                                .First();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.EfCoreExplicitTrackingAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldNotSuggestTrackingBehaviourIfSpecified()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            var db = new TestContext();
                
                            var p = db.Persons
                                .AsNoTracking()
                                .First();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldSuggestTrackingBehaviourOutOfOrder()
        {
            var source =
                """
                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            var db = new TestContext();
                
                            var p = {|#0:db.Persons|}
                                .Include(x => x.Mother)
                                .AsNoTracking()
                                .First();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.EfCoreExplicitTrackingAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }

    }
}
