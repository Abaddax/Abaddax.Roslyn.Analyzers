namespace Abaddax.Roslyn.Analyzers.Framework.Extensions
{
    public static class StringExtensions
    {
        public static bool Contains(this string str, string value, StringComparison comparisonType)
        {
            return str.IndexOf(value, comparisonType) >= 0;
        }
    }
}
