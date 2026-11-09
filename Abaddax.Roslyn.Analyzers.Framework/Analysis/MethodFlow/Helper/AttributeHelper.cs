using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow.Helper
{
    internal static class AttributeHelper
    {
        #region ForwardedParameterAttribute
        public static bool IsForwardedParameterAttribute(AttributeData attributeData)
        {
            return attributeData.HasName("ForwardedParameterAttribute", "Abaddax.Roslyn.Analyzers.Attributes");
        }
        public static string?[] ParseForwardedParameterAttribute(ImmutableArray<TypedConstant> constructorArguments)
        {
            return constructorArguments[0].Values.Select(x => x.Value as string).ToArray();
        }
        #endregion
    }
}
