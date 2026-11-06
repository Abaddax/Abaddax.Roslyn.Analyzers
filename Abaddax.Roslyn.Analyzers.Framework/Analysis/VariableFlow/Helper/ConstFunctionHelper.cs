using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Helper
{
    internal static class ConstFunctionHelper
    {
        public static bool IsConstLikeMethod(this IInvocationOperation methodInvocation)
        {
            var method = methodInvocation.TargetMethod;

            if (method.IsReadOnly)
                return true;

            // Strings are immutable
            if (method.ContainingType.HasName("String", "System"))
                return true;

            // Exclude methods marked as [Pure]
            if (method.GetAttributes("PureAttribute", "System.Diagnostics.Contracts").Any())
                return true;

            return method.Name switch
            {
                // Default object methods
                nameof(ToString) => true,
                nameof(GetHashCode) => true,
                nameof(GetType) => true,
                nameof(Equals) => true,
                nameof(MemberwiseClone) => true,
                //Enumerator
                nameof(IEnumerable.GetEnumerator) => true,
                //IEnumerator
                _ when
                    method.ContainingType.HasName("IEnumerator", "System.Collections") ||
                    method.ContainingType.HasName("IEnumerator", "System.Collections.Generic")
                    => true,
                //ReadOnlyCollections
                _ when
                    method.ExplicitInterfaceImplementations.Any(x => x.ContainingType.Name.Contains("ReadOnly", StringComparison.OrdinalIgnoreCase))
                    => true,
                //TODO: more
                _ => false
            };
        }
    }
}
