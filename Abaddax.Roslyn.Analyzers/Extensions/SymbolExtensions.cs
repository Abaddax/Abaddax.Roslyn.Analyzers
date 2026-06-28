using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class SymbolExtensions
    {
        public static bool HasNamespace(this ITypeSymbol symbol, string @namespace)
        {
            if (symbol.ContainingNamespace == null)
                return symbol.BaseType?.HasNamespace(@namespace) ?? false;
            return symbol.ContainingNamespace.ToDisplayString() == @namespace;
        }

        public static bool HasName(this ITypeSymbol symbol, string name, string @namespace)
        {
            var typeName = symbol.TypeKind != TypeKind.Array
                ? symbol.Name
                : "Array";
            return typeName == name &&
                symbol.HasNamespace(@namespace);
        }
        public static bool HasName(this IMethodSymbol method, string name, string @namespace, string type)
        {
            return method.Name == name &&
                method.ContainingType.HasName(type, @namespace);
        }

        public static ITypeSymbol? GetGenericParameter(this ITypeSymbol type, int index)
        {
            if (type is IArrayTypeSymbol array)
            {
                return index == 0
                    ? array.ElementType
                    : null;
            }
            if (type is not INamedTypeSymbol named)
                return null;
            return named.TypeArguments.ElementAtOrDefault(index);
        }
    }
}
