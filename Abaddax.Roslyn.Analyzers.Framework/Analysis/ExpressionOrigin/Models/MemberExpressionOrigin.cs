using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.ExpressionOrigin.Models
{
    public sealed class MemberExpressionOrigin : ExpressionOrigin
    {
        public ExpressionOrigin Receiver { get; }
        public ISymbol Member { get; }

        public MemberExpressionOrigin(
            ExpressionOrigin receiver,
            ISymbol member)
        {
            Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            Member = member ?? throw new ArgumentNullException(nameof(member));
        }
        public override string ToFullString()
        {
            return Receiver.ToFullString() + "." + Member.Name;
        }
    }
}
