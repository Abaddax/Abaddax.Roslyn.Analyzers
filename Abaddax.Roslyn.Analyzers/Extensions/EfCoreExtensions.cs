using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class EfCoreExtensions
    {
        public static bool IsDbContext(this ITypeSymbol? type)
        {
            var current = type;
            while (current != null)
            {
                if (current.HasName("DbContext", "Microsoft.EntityFrameworkCore"))
                    return true;
                current = current.BaseType;
            }
            return false;
        }
        public static bool IsDbSet(this ITypeSymbol? type)
        {
            var current = type;
            while (current != null)
            {
                if (current.HasName("DbSet", "Microsoft.EntityFrameworkCore"))
                    return true;
                current = current.BaseType;
            }
            return false;
        }

    }
}
