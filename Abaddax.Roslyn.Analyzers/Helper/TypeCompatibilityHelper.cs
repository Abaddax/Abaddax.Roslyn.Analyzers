using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Helper
{
    internal static class TypeCompatibilityHelper
    {
        [Flags]
        [SuppressMessage("Design", "MA0062:Non-flags enums should not be marked with \"FlagsAttribute\"", Justification = "False positive due to `~0'")]
        public enum AlternativityOptions
        {
            DirectMatch = 1 << 1,
            ConversionMatch = 1 << 2,
            UserDefinedConversionMatch = 1 << 3,
            MethodConversionMatch = 1 << 4,
            PropertyConversionMatch = 1 << 5,
            All = ~0,
        }

        /// <summary>
        /// Checks if <paramref name="canditateType"/> is a possible alternative for <paramref name="baselineType"/>
        /// </summary>
        /// <remarks>Will check for <list type="bullet">
        /// <item>Equality</item>
        /// <item>Implicit/Explicit convertablitiy</item>
        /// <item>Convertablitiy via a Property. (e.g. obj.Stream)</item>
        /// <item>Special cases for Array/Span/Memory/ArraySegment</item>
        /// </list></remarks>
        /// <returns></returns>
        public static bool IsTypeAlternative(
            ITypeSymbol canditateType,
            ITypeSymbol baselineType,
            SemanticModel semanticModel,
            int position,
            AlternativityOptions options = AlternativityOptions.All)
        {
            // Exact match
            if (options.HasFlag(AlternativityOptions.DirectMatch) &&
                SymbolEqualityComparer.Default.Equals(canditateType, baselineType))
            {
                return true;
            }
            // Convertible match
            if (options.HasFlag(AlternativityOptions.ConversionMatch) &&
                semanticModel.Compilation.ClassifyConversion(baselineType, canditateType) is { Exists: true } conversion)
            {
                if (conversion.IsImplicit)
                    return true;
                if (options.HasFlag(AlternativityOptions.UserDefinedConversionMatch) && conversion.IsUserDefined)
                    return true;
            }
            // Is Array-like
            if (IsArrayLikeEquivalent(canditateType, baselineType, semanticModel, position, options))
            {
                return true;
            }
            // Has conversion via ToX()/AsX()
            if (options.HasFlag(AlternativityOptions.MethodConversionMatch) &&
                HasConversionMethod(canditateType, baselineType, semanticModel, position, options))
            {
                return true;
            }
            // Has convertsion via property
            if (options.HasFlag(AlternativityOptions.PropertyConversionMatch) &&
                HasConversionProperty(canditateType, baselineType, semanticModel, position, options))
            {
                return true;
            }
            return false;
        }

        private static bool HasConversionMethod(
            ITypeSymbol canditateType,
            ITypeSymbol baselineType,
            SemanticModel semanticModel,
            int position,
            AlternativityOptions options)
        {
            var canditateTypeName = canditateType.Name;
            if (canditateType is IArrayTypeSymbol)
                canditateTypeName = "Array";
            var targetNames = canditateTypeName switch
            {
                //Do not consider ToString a valid conversion!
                "String" => new[] { "AsString" },
                _ => new[] { $"To{canditateTypeName}", $"As{canditateTypeName}" }
            };

            foreach (var targetName in targetNames)
            {
                if (MethodExtensions.ListPotentialAlternatives(baselineType, targetName, semanticModel, position)
                    .Any(x => IsTypeAlternative(x.ReturnType, canditateType, semanticModel, position, options)))
                {
                    return true;
                }
            }
            return false;
        }
        private static bool HasConversionProperty(
            ITypeSymbol canditateType,
            ITypeSymbol baselineType,
            SemanticModel semanticModel,
            int position,
            AlternativityOptions options)
        {
            if (baselineType is not INamedTypeSymbol named)
                return false;

            return named.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(x => x.Name == canditateType.Name)
                .Any(p => IsTypeAlternative(canditateType, p.Type, semanticModel, position,
                    // Only check for direct and implicit type conversions
                    options & ~AlternativityOptions.MethodConversionMatch & ~AlternativityOptions.PropertyConversionMatch));
        }
        private static bool IsArrayLikeEquivalent(
            ITypeSymbol canditateType,
            ITypeSymbol baselineType,
            SemanticModel semanticModel,
            int position,
            AlternativityOptions options)
        {
            if (!IsArrayLikeType(canditateType))
                return false;
            if (!IsArrayLikeType(baselineType))
                return false;

            var canditateElementType = canditateType.GetGenericParameter(0);
            var baselineElementType = baselineType.GetGenericParameter(0);

            return
                canditateElementType is not null &&
                baselineElementType is not null &&
                IsTypeAlternative(canditateElementType, baselineElementType, semanticModel, position,
                    // Only check for direct and implicit type conversions
                    options & ~AlternativityOptions.MethodConversionMatch & ~AlternativityOptions.PropertyConversionMatch);

            static bool IsArrayLikeType(ITypeSymbol type)
            {
                return
                    type.HasName("Array", "System") ||
                    type.HasName("Span", "System") ||
                    type.HasName("ReadOnlySpan", "System") ||
                    type.HasName("Memory", "System") ||
                    type.HasName("ReadOnlyMemory", "System") ||
                    type.HasName("ArraySegment", "System");
            }
        }

        /// <summary>
        /// Constructs the <paramref name="genericMethod"/> given the <paramref name="typeArguments"/>/<paramref name="typeArgumentNullableAnnotations"/>
        /// </summary>
        /// <remarks>Also checks if the generic constrains are fullfilles</remarks>
        public static IMethodSymbol? ConstructGenericMethod(
            IMethodSymbol genericMethod,
            ImmutableArray<ITypeSymbol> typeArguments,
            ImmutableArray<NullableAnnotation> typeArgumentNullableAnnotations,
            SemanticModel semanticModel)
        {
            //Get root generic definition
            while (!SymbolEqualityComparer.Default.Equals(genericMethod.OriginalDefinition, genericMethod))
                genericMethod = genericMethod.OriginalDefinition;

            var method = genericMethod.Construct(typeArguments, typeArgumentNullableAnnotations);
            var genericCount = genericMethod.TypeParameters.Length;
            for (int i = 0; i < genericCount; i++)
            {
                var genericParameter = genericMethod.TypeParameters[i];
                var actualParameter = method.TypeArguments[i];

                if (!FullfillsGenericConstraint(genericParameter, actualParameter, semanticModel))
                    return null;
            }
            return method;
        }

        /// <summary>
        /// Checks if <paramref name="type"/> fullfills all constains of <paramref name="genericParameter"/>
        /// </summary>
        public static bool FullfillsGenericConstraint(ITypeParameterSymbol genericParameter, ITypeSymbol type, SemanticModel semanticModel)
        {
            // Check 'class' cosntraint
            if (genericParameter.HasReferenceTypeConstraint && !type.IsReferenceType)
                return false;
            // Check 'struct' constraint
            if (genericParameter.HasValueTypeConstraint && !type.IsValueType)
                return false;
            // Check 'unmanaged' constraint
            if (genericParameter.HasUnmanagedTypeConstraint && !type.IsUnmanagedType)
                return false;
            // Check 'new()' constraint
            if (genericParameter.HasConstructorConstraint && !HasDefaultConstructor(type))
                return false;
            // Check type constraints
            foreach (var constraintType in genericParameter.ConstraintTypes)
            {
                if (!IsTypeAlternative(type, constraintType, semanticModel, -1,
                    options: AlternativityOptions.DirectMatch | AlternativityOptions.ConversionMatch))
                {
                    return false;
                }
            }
            return true;

            static bool HasDefaultConstructor(ITypeSymbol type)
            {
                if (type.IsValueType)
                    return true;
                if (type.IsAbstract)
                    return false;
                if (type is INamedTypeSymbol namedType)
                {
                    return namedType.InstanceConstructors.Any(x => x.Parameters.IsEmpty && x.DeclaredAccessibility == Accessibility.Public);
                }
                return false;
            }
        }
    }
}
