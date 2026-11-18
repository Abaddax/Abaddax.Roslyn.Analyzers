using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    file sealed class AccessPathEqualityComparer : IEqualityComparer<AccessPath>
    {
        public bool Equals(AccessPath x, AccessPath y)
        {
            if (!AccessPathRoot.EqualityComparer.Equals(x.Root, y.Root))
                return false;
            if (x.Members.Length != y.Members.Length)
                return false;
            for (int i = 0; i < x.Members.Length; i++)
            {
                if (!AccessSymbol.EqualityComparer.Equals(x.Members[i], y.Members[i]))
                    return false;
            }
            return true;
        }
        public int GetHashCode(AccessPath obj)
        {
            //Generate hash using ValueTuple
            return (
                    AccessPathRoot.EqualityComparer.GetHashCode(obj.Root),
                    obj.Members.Length
                ).GetHashCode();
        }
    }
    /// <summary>
    /// The chain of member accesses starting from a root variable symbol.
    /// </summary>
    /// <remarks>
    /// Example: x -> .Prop1 -> .Prop2
    /// </remarks>
    internal readonly struct AccessPath
    {
        public static readonly IEqualityComparer<AccessPath> EqualityComparer = new AccessPathEqualityComparer();

        public AccessPathRoot Root { get; }
        public ImmutableArray<AccessSymbol> Members { get; }

        private AccessPath(AccessPathRoot root, ImmutableArray<AccessSymbol> members)
        {
            Root = root;
            Members = members;
        }

        public static AccessPath FromRoot(ISymbol root, IOperation? accessOperation = null)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            return new(new AccessPathRoot(new AccessSymbol(root, accessOperation)), ImmutableArray<AccessSymbol>.Empty);
        }
        public static AccessPath FromRoot(AccessPathRoot root)
        {
            return new(root, ImmutableArray<AccessSymbol>.Empty);
        }

        public AccessPath Append(AccessSymbol member) => new(Root, Members.Add(member));
        public AccessPath AppendRange(IEnumerable<AccessSymbol> members) => new(Root, Members.AddRange(members));

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Root);
            foreach (var member in Members)
            {
                sb.Append('.');
                sb.Append(member.ToString());
            }
            return sb.ToString();
        }
    }
}
