using Abaddax.Roslyn.Analyzers.Analyzers;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.Analyzers
{
    public sealed class EfCoreThenIncludeFormattingAnalyzerTests
        : AnalyzerTestBase<EfCoreThenIncludeFormattingAnalyzer>
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
                        public static extern IIncludableQueryable<TEntity, TProperty> Include<TEntity, TProperty>(
                            this IQueryable<TEntity> source,
                            Expression<Func<TEntity, TProperty>> navigationPropertyPath)
                            where TEntity : class;
                        public static extern IIncludableQueryable<TEntity, TProperty> ThenInclude<TEntity, TPreviousProperty, TProperty>(
                            this IIncludableQueryable<TEntity, TPreviousProperty> source,
                            Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath) 
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
            base.SetupTestState(state);
        }

        [Test]
        public async Task ShouldSuggestIndentationIfSameLevel()
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
                
                            var p = {|#0:db.Persons
                                .Include(x => x.Mother)
                                .ThenInclude(x => x.Father)|}
                                .First();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source,
                new DiagnosticResult(AnalyzerIdentifiers.EfCoreThenIncludeFormattingAnalyzer, DiagnosticSeverity.Info)
                    .WithLocation(0)
                );
        }
        [Test]
        public async Task ShouldNotSuggestIndentationIfSameLine()
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
                
                            var p = db.Persons.Include(x => x.Mother).ThenInclude(x => x.Father).ThenInclude(x => x.Father)
                                .First();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }
        [Test]
        public async Task ShouldNotSuggestIndentationIfOneMoreLevel()
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
                                .Include(x => x.Mother)
                                    .ThenInclude(x => x.Father)
                                        .ThenInclude(x => x.Father)
                                .First();
                        }
                    }
                }
                """;
            await VerifyAnalyzerAsync(source);
        }

    }
}
