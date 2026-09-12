using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class AttributeExtensions
    {
        public static bool HasName(this AttributeData attributeData, string attributeName, string @namespace)
        {
            return attributeData.AttributeClass?.HasName(attributeName, @namespace) ?? false;
        }
        public static IEnumerable<AttributeData> WithName(this IEnumerable<AttributeData> attributes, string attributeName, string @namespace)
        {
            return attributes
                .Where(x => x.HasName(attributeName, @namespace));
        }
        public static T ParseConstructorArguments<T>(this AttributeData attributeData, Func<ImmutableArray<TypedConstant>, T> parser)
        {
            return parser.Invoke(attributeData.ConstructorArguments);
        }
        public static IEnumerable<T> ParseConstructorArguments<T>(this IEnumerable<AttributeData> attributes, Func<ImmutableArray<TypedConstant>, T> parser)
        {
            return attributes
                .Select(x => x.ParseConstructorArguments(parser));
        }

        public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, string attributeName, string @namespace)
        {
            return symbol.GetAttributes(x => x.HasName(attributeName, @namespace));
        }
        public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, Func<AttributeData, bool> predicate)
        {
            return symbol.GetAttributes()
                .Where(predicate);
        }
        public static IEnumerable<AttributeData> GetReturnTypeAttributes(this IMethodSymbol method, string attributeName, string @namespace)
        {
            return method.GetReturnTypeAttributes(x => x.HasName(attributeName, @namespace));
        }
        public static IEnumerable<AttributeData> GetReturnTypeAttributes(this IMethodSymbol method, Func<AttributeData, bool> predicate)
        {
            if (method.ReducedFrom != null)
                method = method.ReducedFrom;
            return method.GetReturnTypeAttributes()
                .Where(predicate);
        }


    }
}
