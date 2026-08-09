using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Text.RegularExpressions;

namespace Abaddax.Roslyn.Analyzers.Helper
{
    internal static class LinqTraversalHelper
    {
        public static IOperation? GetForwardedParameter(
            IInvocationOperation linqMethodInvocation,
            IArgumentOperation linqMethodArgument,
            int lambdaParameterIndex)
        {
            var linqMethod = linqMethodInvocation.TargetMethod;
            var linqParameter = linqMethodArgument.Parameter;
            if (linqParameter == null)
                return null;

            // Get parameter index inside linq method
            var linqParameterIndex = linqMethod.Parameters.IndexOf(linqParameter, 0, linqMethod.Parameters.Length, SymbolEqualityComparer.Default);
            if (linqParameterIndex < 0)
                return null; // Not from the linq method?

            // Get generic method definition
            linqMethod = linqMethod.OriginalDefinition;

            if (linqMethod.ContainingType.HasName("Enumerable", "System.Linq"))
            {
                return GetEnumerableForwardedParameter(
                    linqMethod,
                    linqMethodInvocation,
                    linqParameterIndex,
                    lambdaParameterIndex);
            }
            return null;
        }

        private static IOperation? GetEnumerableForwardedParameter(
            IMethodSymbol linqMethod,
            IInvocationOperation linqMethodInvocation,
            int linqParameterIndex,
            int lambdaParameterIndex)
        {
#pragma warning disable IDE1006 // Namingstyle
            const string IEnumerable = "System.Collections.Generic.IEnumerable";
            const string IOrderedEnumerable = "System.Linq.IOrderedEnumerable";
            const string Func = "System.Func";
            const string IEqualityComparer = "System.Collections.Generic.IEqualityComparer";
            const string IComparer = "System.Collections.Generic.IComparer";
            const string Nullable = "System.Nullable";
            const string Boolean = "bool";
            const string Int32 = "int";
            const string Int64 = "long";
            const string Single = "float";
            const string Double = "double";
            const string Decimal = "decimal";
#pragma warning restore IDE1006 // Namingstyle

            var operation = linqMethod.Name switch
            {
                "Aggregate" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (2, 1) when
                        IsFunctionMatch(linqMethod, $"Aggregate<TSource,TAccumulate,TResult>({IEnumerable}<TSource>, TAccumulate, {Func}<TAccumulate,TSource,TAccumulate>, {Func}<TAccumulate,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"Aggregate<TSource,TAccumulate>({IEnumerable}<TSource>, TAccumulate, {Func}<TAccumulate,TSource,TAccumulate>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (1, 1) when
                        IsFunctionMatch(linqMethod, $"Aggregate<TSource>({IEnumerable}<TSource>, {Func}<TSource,TSource,TSource>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "AggregateBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) or (3, 1) when
                        IsFunctionMatch(linqMethod, $"AggregateBy<TSource,TKey,TAccumulate>({IEnumerable}<TSource>, {Func}<TSource, TKey>, {Func}<TKey,TAccumulate>, {Func}<TAccumulate,TSource,TAccumulate>, {IEqualityComparer}<TKey>)") ||
                        IsFunctionMatch(linqMethod, $"AggregateBy<TSource,TKey,TAccumulate>({IEnumerable}<TSource>, {Func}<TSource, TKey>, TAccumulate, {Func}<TAccumulate,TSource,TAccumulate>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "All" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"All<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "Any" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"Any<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "Average" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) => linqMethod.Parameters.ElementAtOrDefault(1)?.Type.GetGenericParameter(1)?.ToDisplayString() switch
                    {
                        Int32 when
                            IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int32}>)") ||
                            IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Int32}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Int64 when
                            IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int64}>)") ||
                            IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Int64}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Single when
                           IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Single}>)") ||
                           IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Single}>>)")
                           => linqMethodInvocation.Arguments[0].Value,
                        Double when
                            IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Double}>)") ||
                            IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Double}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Decimal when
                            IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Decimal}>)") ||
                            IsFunctionMatch(linqMethod, $"Average<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Decimal}>>)")
                           => linqMethodInvocation.Arguments[0].Value,
                        _ => null
                    },
                    _ => null
                },
                "Count" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"Count<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "CountBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"CountBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "DistinctBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"DistinctBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"DistinctBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "ExceptBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (2, 0) when
                        IsFunctionMatch(linqMethod, $"ExceptBy<TSource,TKey>({IEnumerable}<TSource>, {IEnumerable}<TKey>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"ExceptBy<TSource,TKey>({IEnumerable}<TSource>, {IEnumerable}<TKey>, {Func}<TSource,TKey>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "First" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"First<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "FirstOrDefault" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"FirstOrDefault<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)") ||
                        IsFunctionMatch(linqMethod, $"FirstOrDefault<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>, TSource)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "GroupBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"GroupBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"GroupBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (1, 0) or (2, 0) when
                        IsFunctionMatch(linqMethod, $"GroupBy<TSource,TKey,TElement,TResult>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TSource,TElement>, {Func}<TKey,{IEnumerable}<TElement>,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"GroupBy<TSource,TKey,TElement,TResult>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TSource,TElement>, {Func}<TKey,{IEnumerable}<TElement>,TResult>, {IEqualityComparer}<TKey>)") ||
                        IsFunctionMatch(linqMethod, $"GroupBy<TSource,TKey,TElement>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TSource,TElement>)") ||
                        IsFunctionMatch(linqMethod, $"GroupBy<TSource,TKey,TElement>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TSource,TElement>, {IEqualityComparer}<TKey>)") ||
                        IsFunctionMatch(linqMethod, $"GroupBy<TSource,TKey,TResult>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TKey,{IEnumerable}<TSource>,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"GroupBy<TSource,TKey,TResult>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TKey,{IEnumerable}<TSource>,TResult>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "GroupJoin" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (2, 0) or (4, 0) when
                        IsFunctionMatch(linqMethod, $"GroupJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,{IEnumerable}<TInner>,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"GroupJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,{IEnumerable}<TInner>,TResult>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (3, 0) or (4, 1) when
                        IsFunctionMatch(linqMethod, $"GroupJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,{IEnumerable}<TInner>,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"GroupJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,{IEnumerable}<TInner>,TResult>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[1].Value,
                    _ => null
                },
                "IntersectBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (2, 0) when
                        IsFunctionMatch(linqMethod, $"IntersectBy<TSource,TKey>({IEnumerable}<TSource>, {IEnumerable}<TKey>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"IntersectBy<TSource,TKey>({IEnumerable}<TSource>, {IEnumerable}<TKey>, {Func}<TSource,TKey>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "Join" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (2, 0) or (4, 0) when
                        IsFunctionMatch(linqMethod, $"Join<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"Join<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (3, 0) or (4, 1) when
                        IsFunctionMatch(linqMethod, $"Join<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"Join<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[1].Value,
                    _ => null
                },
                "Last" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"Last<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "LastOrDefault" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"LastOrDefault<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)") ||
                        IsFunctionMatch(linqMethod, $"LastOrDefault<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>, TSource)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "LeftJoin" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (2, 0) or (4, 0) when
                        IsFunctionMatch(linqMethod, $"LeftJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"LeftJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (3, 0) or (4, 1) when
                        IsFunctionMatch(linqMethod, $"LeftJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"LeftJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[1].Value,
                    _ => null
                },
                "LongCount" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"LongCount<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "Max" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) => linqMethod.Parameters.ElementAtOrDefault(1)?.Type.GetGenericParameter(1)?.ToDisplayString() switch
                    {
                        "TResult" when
                            IsFunctionMatch(linqMethod, $"Max<TSource,TResult>({IEnumerable}<TSource>, {Func}<TSource,TResult>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Int32 when
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int32}>)") ||
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Int32}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Int64 when
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int64}>)") ||
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Int64}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Single when
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Single}>)") ||
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Single}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Double when
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Double}>)") ||
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Double}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Decimal when
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Decimal}>)") ||
                            IsFunctionMatch(linqMethod, $"Max<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Decimal}>>)")
                           => linqMethodInvocation.Arguments[0].Value,
                        _ => null
                    },
                    _ => null
                },
                "MaxBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"MaxBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"MaxBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {IComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "Min" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) => linqMethod.Parameters.ElementAtOrDefault(1)?.Type.GetGenericParameter(1)?.ToDisplayString() switch
                    {
                        "TResult" when
                            IsFunctionMatch(linqMethod, $"Min<TSource,TResult>({IEnumerable}<TSource>, {Func}<TSource,TResult>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Int32 when
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int32}>)") ||
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Int32}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Int64 when
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int64}>)") ||
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Int64}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Single when
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Single}>)") ||
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Single}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Double when
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Double}>)") ||
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Double}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Decimal when
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Decimal}>)") ||
                            IsFunctionMatch(linqMethod, $"Min<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Decimal}>>)")
                           => linqMethodInvocation.Arguments[0].Value,
                        _ => null
                    },
                    _ => null
                },
                "MinBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"MinBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"MinBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {IComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "OrderBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"OrderBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"OrderBy<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {IComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "OrderByDescending" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"OrderByDescending<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"OrderByDescending<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {IComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "RightJoin" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (2, 0) or (4, 0) when
                        IsFunctionMatch(linqMethod, $"RightJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"RightJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (3, 0) or (4, 1) when
                        IsFunctionMatch(linqMethod, $"RightJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"RightJoin<TOuter,TInner,TKey,TResult>({IEnumerable}<TOuter>, {IEnumerable}<TInner>, {Func}<TOuter,TKey>, {Func}<TInner,TKey>, {Func}<TOuter,TInner,TResult>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[1].Value,
                    _ => null
                },
                "Select" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"Select<TSource,TResult>({IEnumerable}<TSource>, {Func}<TSource,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"Select<TSource,TResult>({IEnumerable}<TSource>, {Func}<TSource,{Int32},TResult>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "SelectMany" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"SelectMany<TSource,TResult>({IEnumerable}<TSource>, {Func}<TSource,{IEnumerable}<TResult>>)") ||
                        IsFunctionMatch(linqMethod, $"SelectMany<TSource,TResult>({IEnumerable}<TSource>, {Func}<TSource,{Int32},{IEnumerable}<TResult>>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (1, 0) or (2, 0) when
                        IsFunctionMatch(linqMethod, $"SelectMany<TSource,TCollection,TResult>({IEnumerable}<TSource>, {Func}<TSource,{IEnumerable}<TCollection>>, {Func}<TSource,TCollection,TResult>)") ||
                        IsFunctionMatch(linqMethod, $"SelectMany<TSource,TCollection,TResult>({IEnumerable}<TSource>, {Func}<TSource,{Int32},{IEnumerable}<TCollection>>, {Func}<TSource,TCollection,TResult>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "Single" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"Single<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "SingleOrDefault" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"SingleOrDefault<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)") ||
                        IsFunctionMatch(linqMethod, $"SingleOrDefault<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>, TSource)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "SkipWhile" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"SkipWhile<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)") ||
                        IsFunctionMatch(linqMethod, $"SkipWhile<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int32},{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "Sum" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) => linqMethod.Parameters.ElementAtOrDefault(1)?.Type.GetGenericParameter(1)?.ToDisplayString() switch
                    {
                        Int32 when
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int32}>)") ||
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Int32}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Int64 when
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int64}>)") ||
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Int64}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Single when
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Single}>)") ||
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Single}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Double when
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Double}>)") ||
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Double}>>)")
                            => linqMethodInvocation.Arguments[0].Value,
                        Decimal when
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Decimal}>)") ||
                            IsFunctionMatch(linqMethod, $"Sum<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Nullable}<{Decimal}>>)")
                           => linqMethodInvocation.Arguments[0].Value,
                        _ => null
                    },
                    _ => null
                },
                "TakeWhile" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"TakeWhile<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)") ||
                        IsFunctionMatch(linqMethod, $"TakeWhile<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int32},{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "ThenBy" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"ThenBy<TSource,TKey>({IOrderedEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"ThenBy<TSource,TKey>({IOrderedEnumerable}<TSource>, {Func}<TSource,TKey>, {IComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "ThenByDescending" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"ThenByDescending<TSource,TKey>({IOrderedEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"ThenByDescending<TSource,TKey>({IOrderedEnumerable}<TSource>, {Func}<TSource,TKey>, {IComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "ToDictionary" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"ToDictionary<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"ToDictionary<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (1, 0) or (2, 0) when
                        IsFunctionMatch(linqMethod, $"ToDictionary<TSource,TKey,TElement>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TSource,TElement>)") ||
                        IsFunctionMatch(linqMethod, $"ToDictionary<TSource,TKey,TElement>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TSource,TElement>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "ToLookup" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"ToLookup<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>)") ||
                        IsFunctionMatch(linqMethod, $"ToLookup<TSource,TKey>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (1, 0) or (2, 0) when
                        IsFunctionMatch(linqMethod, $"ToLookup<TSource,TKey,TElement>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TSource,TElement>)") ||
                        IsFunctionMatch(linqMethod, $"ToLookup<TSource,TKey,TElement>({IEnumerable}<TSource>, {Func}<TSource,TKey>, {Func}<TSource,TElement>, {IEqualityComparer}<TKey>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "UnionBy" => null, //Not supported
                "Where" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (1, 0) when
                        IsFunctionMatch(linqMethod, $"Where<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Boolean}>)") ||
                        IsFunctionMatch(linqMethod, $"Where<TSource>({IEnumerable}<TSource>, {Func}<TSource,{Int32},{Boolean}>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    _ => null
                },
                "Zip" => (linqParameterIndex, lambdaParameterIndex) switch
                {
                    (2, 0) when
                        IsFunctionMatch(linqMethod, $"Zip<TFirst,TSecond,TResult>({IEnumerable}<TFirst>, {IEnumerable}<TSecond>, {Func}<TFirst,TSecond,TResult>)")
                        => linqMethodInvocation.Arguments[0].Value,
                    (2, 1) when
                        IsFunctionMatch(linqMethod, $"Zip<TFirst,TSecond,TResult>({IEnumerable}<TFirst>, {IEnumerable}<TSecond>, {Func}<TFirst,TSecond,TResult>)")
                        => linqMethodInvocation.Arguments[1].Value,
                    _ => null
                },
                _ => null
            };
            return operation;
        }

        private static readonly Regex _MethodSignatureRegex = new Regex(
            @"^\s*(?<name>[A-Za-z_]\w*)(?:<(?<generics>.+?)>)?\((?<parameters>.*)\)\s*;?\s*$",
            RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex _TypeSignatureRegex = new Regex(
            @"^(?<namespace>(?:[A-Za-z_]\w*\.)*)(?<typename>[A-Za-z_]\w*)(?:<(?<generics>.+?)>)?$",
            RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Checks if <paramref name="method"/> matches <paramref name="methodSignature"/>
        /// </summary>
        private static bool IsFunctionMatch(IMethodSymbol method,
           string methodSignature)
        {
            var match = _MethodSignatureRegex.Match(methodSignature);
            if (!match.Success)
                return false;

            var name = match.Groups["name"].Value;
            var generics = match.Groups["generics"].Value;
            var parameters = match.Groups["parameters"].Value;

            // Check name
            if (method.Name != name)
                return false;

            // Check generics
            var genericParameters = SplitTopLevel(generics).ToArray();
            if (method.TypeParameters.Length != genericParameters.Length)
                return false;
            for (int i = 0; i < method.TypeParameters.Length; i++)
            {
                if (method.TypeParameters[i].Name != genericParameters[i])
                    return false;
            }

            //Check parameters
            var functionParameters = SplitTopLevel(parameters).ToArray();
            if (method.Parameters.Length != functionParameters.Length)
                return false;
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (!IsParameterTypeMatch(method.Parameters[i].Type, functionParameters[i]))
                    return false;
            }

            // All checks succeeded
            return true;
        }
        /// <summary>
        /// Checks is <paramref name="type"/> matches <paramref name="typeSignature"/>
        /// </summary>
        private static bool IsParameterTypeMatch(ITypeSymbol type, string typeSignature)
        {
            var match = _TypeSignatureRegex.Match(typeSignature);
            if (!match.Success)
                return false;

            var @namespace = match.Groups["namespace"].Value;
            var typename = match.Groups["typename"].Value;
            var generics = match.Groups["generics"].Value;

            // Namespace includes trailing '.' -> remove
            if (!string.IsNullOrEmpty(@namespace))
                @namespace = @namespace.Substring(0, @namespace.Length - 1);

            // Check name and namespace
            if (!type.HasName(typename, @namespace))
                return false;

            // Check generics
            var genericParameters = SplitTopLevel(generics).ToArray();
            int i = 0;
            while (true)
            {
                var genericType = type.GetGenericParameter(i);
                if (genericType == null && genericParameters.Length == i)
                    break; //All generic parameters checked
                if (genericType == null || genericParameters.Length - 1 < i)
                    return false;
                if (genericType.ToDisplayString() != genericParameters[i])
                    return false;
                i++;
            }

            // All checks succeeded
            return true;
        }
        /// <summary>
        /// Splits comma-separated lists while respecting nested generic brackets.
        /// </summary>
        private static IEnumerable<string> SplitTopLevel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            int depth = 0;
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case '<':
                        depth++;
                        break;
                    case '>':
                        depth--;
                        break;
                    case ',' when depth == 0:
                        yield return text.Substring(start, i - start).Trim();
                        start = i + 1;
                        break;
                }
            }
            yield return text.Substring(start).Trim();
        }
    }
}
