using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    file sealed class AccessPathRootEqualityComparer : IEqualityComparer<AccessPathRoot>
    {
        public bool Equals(AccessPathRoot x, AccessPathRoot y)
        {
            if (x.Access == null && y.Access == null)
                return true;
            if (x.Access == null || y.Access == null)
                return false;
            return AccessSymbol.EqualityComparer.Equals(x.Access.Value, y.Access.Value);
        }
        public int GetHashCode(AccessPathRoot obj)
        {
            if (obj.Access == null)
                return 0;
            return AccessSymbol.EqualityComparer.GetHashCode(obj.Access.Value);
        }
    }

    /// <summary>
    /// The root of a variable symbol.
    /// </summary>
    /// <remarks>
    /// Example: x, this, ...
    /// </remarks>
    internal readonly struct AccessPathRoot
    {
        public static readonly IEqualityComparer<AccessPathRoot> EqualityComparer = new AccessPathRootEqualityComparer();

        public static readonly AccessPathRoot This = new(null, true);
        public static AccessPathRoot Static(ITypeSymbol type) => new(new AccessSymbol(type), false);

        [MemberNotNullWhen(false, nameof(Access))]
        public bool IsThis => Access == null;
        [MemberNotNullWhen(true, nameof(Access))]
        public bool IsStatic => Access?.Symbol is ITypeSymbol;

        public AccessSymbol? Access { get; }

        public AccessPathRoot(AccessSymbol access)
            : this(access, false) { }
        private AccessPathRoot(AccessSymbol? access, bool isThis)
        {
            if (!isThis && access == null)
                throw new ArgumentNullException(nameof(access));
            Access = access;
        }

        public override string ToString()
        {
            return Access?.ToString() ?? "this";
        }
    }
}
