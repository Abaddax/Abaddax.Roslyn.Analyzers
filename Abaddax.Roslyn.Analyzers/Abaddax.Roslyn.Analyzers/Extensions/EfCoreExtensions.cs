using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class EfCoreExtensions
    {
        public static bool IsCalledFromDbContext(this ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var current = expression;
            while (true)
            {
                if (current is InvocationExpressionSyntax invocation)
                    current = invocation.Expression;
                else if (current is MemberAccessExpressionSyntax memberAccess)
                    current = memberAccess.Expression;
                else
                    break;
            }
            var type = semanticModel.GetTypeInfo(current, cancellationToken).Type;
            return type.IsDbContext();
        }
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
    }
}
