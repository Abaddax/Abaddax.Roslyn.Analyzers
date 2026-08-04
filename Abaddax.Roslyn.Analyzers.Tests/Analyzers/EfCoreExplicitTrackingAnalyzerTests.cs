using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
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
                namespace Microsoft.EntityFrameworkCore
                {
                    public class DbContext;
                    public class DbSet<TEntity> : IQueryable<TEntity>
                        where TEntity : class
                    {
                        Type IQueryable.ElementType => throw new NotImplementedException();
                        Expression IQueryable.Expression => throw new NotImplementedException();
                        IQueryProvider IQueryable.Provider => throw new NotImplementedException();
                        IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator() => throw new NotImplementedException();
                        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
                    }
                    public static class EntityFrameworkQueryableExtensions
                    {
                        #pragma warning disable CS0626
                        public static extern IQueryable<TEntity> AsNoTracking<TEntity>(
                            this IQueryable<TEntity> source)
                            where TEntity : class;
                        public static extern IQueryable<TEntity> AsNoTrackingWithIdentityResolution<TEntity>(
                            this IQueryable<TEntity> source)
                            where TEntity : class;
                        public static extern IQueryable<TEntity> AsTracking<TEntity>(
                            this IQueryable<TEntity> source)
                            where TEntity : class;
                        public static extern IIncludableQueryable<TEntity, TProperty> Include<TEntity, TProperty>(
                            this IQueryable<TEntity> source,
                            Expression<Func<TEntity, TProperty>> navigationPropertyPath)
                            where TEntity : class;
                        public static extern Task<TSource> FirstAsync<TSource>(
                            this IQueryable<TSource> source,
                            CancellationToken cancellationToken = default);
                    }
                    namespace Query
                    {
                        public interface IIncludableQueryable<out TEntity, out TProperty> : IQueryable<TEntity>;
                    }
                }
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
                        public DbSet<TestEntity> Persons { get; set; } = new();
                    }
                }
                """);
            state.AnalyzerConfigFiles.Add(("/.editorconfig",
                    $"""
                    root = true

                    [*.cs]
                    dotnet_code_quality.{AnalyzerIdentifiers.EfCoreDereferencePossibleNullReferenceSuppression}.enabled = true

                    [*.cs]
                    dotnet_diagnostic.CS1591.severity = none
                    """));
            base.SetupTestState(state);

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
