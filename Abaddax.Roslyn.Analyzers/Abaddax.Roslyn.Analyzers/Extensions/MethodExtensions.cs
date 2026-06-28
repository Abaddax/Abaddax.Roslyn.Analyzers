using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class MethodExtensions
    {
        public static bool IsEntryPoint(this IMethodSymbol method, SyntaxNodeAnalysisContext context)
        {
            //Entry point is always static
            if (!method.IsStatic)
                return false;
            var entryPoint = context.Compilation.GetEntryPoint(context.CancellationToken);
            if (SymbolEqualityComparer.Default.Equals(entryPoint, method))
                return true;
            return false;
        }

        public static bool IsAsyncCompatibleReturnType(this ITypeSymbol type)
        {
            if (type.HasName("Task", "System.Threading.Tasks"))
                return true;
            if (type.HasName("ValueTask", "System.Threading.Tasks"))
                return true;
            if (type.HasName("IAsyncEnumerable", "System.Collections.Generic"))
                return true;
            return false;
        }
        public static bool IsTaskedMethodDeclaration(this IMethodSymbol method)
        {
            // Check return type: Task or ValueTask
            var returnType = method.ReturnType;
            if (!returnType.IsAsyncCompatibleReturnType())
            {
                return false;
            }

            // Do not warn about overloads
            if (method.IsImplicitlyDeclared ||
                method.IsOverride)
            {
                return false;
            }

            return true;
        }
        public static bool IsAsyncMethod(this IMethodSymbol method)
        {
            if (method.IsAsync)
                return true;

            // Check return type: Task or ValueTask
            var returnType = method.ReturnType;
            if (!returnType.IsAsyncCompatibleReturnType())
            {
                return false;
            }

            return true;
        }

        public static bool IsInsideController(this IMethodSymbol method)
        {
            var current = method.ContainingType;
            while (current != null)
            {
                if (current.HasName("ControllerBase", "Microsoft.AspNetCore.Mvc"))
                    return true;
                current = current.BaseType;
            }
            return false;
        }

        public static IEnumerable<IMethodSymbol> ListPotentialAlternatives(ITypeSymbol receiverType, string targetName, SemanticModel model, int position)
        {
            //Check type and bases
            var current = receiverType;
            while (current != null)
            {
                foreach (var candidate in current.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(x => x.Name == targetName))
                {
                    yield return candidate;
                }
                current = current.BaseType;
            }

            //Check interfaces
            foreach (var interfaceType in receiverType.AllInterfaces)
            {
                foreach (var candidate in interfaceType.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(x => x.Name == targetName))
                {
                    yield return candidate;
                }
            }

            //Check extensions
            var extensionMethods = MethodExtensions.ListExtensionMethodsFor(receiverType, model, position);
            foreach (var candidate in extensionMethods
                .Where(x => x.Name == targetName))
            {
                yield return candidate;
            }
        }
        public static IMethodSymbol[] ListExtensionMethodsFor(ITypeSymbol type, SemanticModel model, int position)
        {
            return model.LookupSymbols(position)
              .Where(x => x.IsStatic)
              .OfType<INamedTypeSymbol>()
              .SelectMany(x => x.GetMembers())
              .OfType<IMethodSymbol>()
              .Where(x => x.IsExtensionMethod)
              .Where(x => x.ReduceExtensionMethod(type) != null)
              .ToArray();
        }
    }
}
