namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class EnumerableExtensions
    {
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
    }
}
