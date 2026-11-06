using System.Diagnostics;

namespace Abaddax.Roslyn.Analyzers.Framework.Extensions
{
    public static class EnumerableExtensions
    {
        [DebuggerStepThrough]
        public static T? ExactlyOneOrDefault<T>(this IEnumerable<T> source)
        {
            int found = 0;
            T first = default!;
            foreach (var item in source.Take(2))
            {
                found++;
                if (found == 1)
                    first = item;
                else
                    break;
            }
            if (found == 1)
                return first;
            return default;
        }
        [DebuggerStepThrough]
        public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        {
            return source
                .Where(x => x != null)
                .Select(x => x!);
        }
        [DebuggerStepThrough]
        public static IEnumerable<string> WhereNotNullOrEmpty(this IEnumerable<string?> source)
        {
            return source
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!);
        }
        [DebuggerStepThrough]
        public static IEnumerable<string> WhereNotNullOrWhiteSpace(this IEnumerable<string?> source)
        {
            return source
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!);
        }
    }
}
