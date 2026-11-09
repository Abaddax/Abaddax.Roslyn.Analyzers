using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Helper
{
    internal static class ConstFunctionHelper
    {
        public static bool IsConstLikeMethod(this IInvocationOperation methodInvocation)
        {
            var method = methodInvocation.TargetMethod;

            if (method.IsReadOnly)
                return true;
            if (method.ContainingType.IsReadOnly)
                return true;

            // Exclude known readonly types
            if (IsReadOnlyType(method.ContainingType))
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
                //Enumerable.GetEnumerator
                nameof(IEnumerable.GetEnumerator) => true,
                //IEnumerator-Methods
                _ when
                    method.ContainingType.HasName("IEnumerator", "System.Collections") ||
                    method.ContainingType.HasName("IEnumerator", "System.Collections.Generic")
                    => true,
                //ReadOnlyCollection-Interface implementation
                _ when
                    method.ExplicitInterfaceImplementations.Any(x => x.ContainingType.Name.Contains("IReadOnly", StringComparison.OrdinalIgnoreCase))
                    => true,
                //TODO: Add more
                _ => false
            };
        }
        public static bool IsConstLikeArgument(this IArgumentOperation argument)
        {
            var parameter = argument.Parameter;
            if (parameter == null)
                return false;
            // Exclude parameters passed as in/ref readonly
            if (parameter.RefKind is RefKind.In or RefKind.RefReadOnly or RefKind.RefReadOnlyParameter)
                return true;

            // Exclude known readonly types
            if (IsReadOnlyType(parameter.Type))
                return true;

            // Exclude parameters marked as [Pure]
            if (parameter.GetAttributes("PureAttribute", "System.Diagnostics.Contracts").Any())
                return true;

            return false;
        }


        private static bool IsReadOnlyType(ITypeSymbol type)
        {
            // Covers readonly structs like ReadOnlySpan, ReadOnlyMemory, DateTime, ...
            if (type.IsReadOnly)
                return true;

            return type.Name switch
            {
                // Strings are immutable
                "String" when
                    type.HasName("String", "System")
                    => true,
                "Uri" when
                    type.HasName("Uri", "System")
                    => true,
                "Version" when
                    type.HasName("Version", "System")
                    => true,
                // ReadOnlyCollections
                _ when
                    type.Name.Contains("ReadOnly", StringComparison.OrdinalIgnoreCase) &&
                    type.HasNamespace("System.Collections.ObjectModel")
                    => true,
                // ImmutableCollections
                _ when
                    type.Name.Contains("Immutable", StringComparison.OrdinalIgnoreCase) &&
                    type.HasNamespace("System.Collections.Immutable")
                    => true,
                // FrozenCollections
                _ when
                    type.Name.Contains("Frozen", StringComparison.OrdinalIgnoreCase) &&
                    type.HasNamespace("System.Collections.Frozen")
                    => true,
                //TODO: Add more
                _ => false
            };
        }
    }
}
