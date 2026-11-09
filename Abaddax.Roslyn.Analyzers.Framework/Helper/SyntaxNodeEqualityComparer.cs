using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Helper
{
    public class SyntaxNodeEqualityComparer : IEqualityComparer<SyntaxNode?>
    {
        /// <summary>
        /// Check for equality of two <see cref="SyntaxNode"/>
        /// </summary>
        public static readonly IEqualityComparer<SyntaxNode?> Default = new SyntaxNodeEqualityComparer(false);
        /// <summary>
        /// Check for equality of two  <see cref="SyntaxNode"/> including their <see cref="SyntaxNode.Parent"/>
        /// </summary>
        public static readonly IEqualityComparer<SyntaxNode?> IncludeParents = new SyntaxNodeEqualityComparer(true);


        private readonly bool _includeParentSyntax;
        private SyntaxNodeEqualityComparer(bool includeParentSyntax)
        {
            _includeParentSyntax = includeParentSyntax;
        }
        public bool Equals(SyntaxNode? x, SyntaxNode? y)
        {
            if (x is null || y is null)
                return x is null && y is null;
            if (!x.Span.Equals(y.Span))
                return false;
            if (!x.IsEquivalentTo(y))
                return false;
            if (!_includeParentSyntax)
                return true;
            return Equals(x.Parent, y.Parent);
        }
        public int GetHashCode(SyntaxNode? obj)
        {
            return obj?.GetHashCode() ?? 0;
        }
    }
}
