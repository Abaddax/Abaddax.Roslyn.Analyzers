using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Helper
{
    internal static class AttributeHelper
    {
        #region EfCorePropertyIncludedAttribute
        public static bool IsEfCorePropertyIncludedAttribute(AttributeData attributeData)
        {
            return attributeData.HasName("EfCorePropertyIncludedAttribute", "Abaddax.Roslyn.Analyzers.Attributes");
        }
        public static string ParseEfCorePropertyIncludedAttribute(ImmutableArray<TypedConstant> constructorArguments)
        {
            return constructorArguments[0].Value as string ?? throw new InvalidOperationException("EfCorePropertyIncludedAttribute can not be constructed with 'null'");
        }
        #endregion
    }
}
