using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.ExpressionOrigin.Models
{
    public sealed class InvocationSyntaxExpressionOrigin : ForwardingExpressionOrigin
    {
        public InvocationExpressionSyntax Invocation { get; }
        public InvocationSyntaxExpressionOrigin(ExpressionOrigin receiver, InvocationExpressionSyntax invocation)
            : base(receiver)
        {
            Invocation = invocation ?? throw new ArgumentNullException(nameof(invocation));
        }
        public override string ToFullString()
        {
            return Receiver.ToFullString() + Invocation.ArgumentList.ToFullString();
        }
    }
}
