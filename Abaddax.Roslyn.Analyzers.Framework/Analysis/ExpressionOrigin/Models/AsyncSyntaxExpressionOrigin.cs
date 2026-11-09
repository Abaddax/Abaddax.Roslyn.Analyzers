namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.ExpressionOrigin.Models
{
    public sealed class AsyncSyntaxExpressionOrigin : ForwardingExpressionOrigin
    {
        public AsyncSyntaxExpressionOrigin(ExpressionOrigin receiver)
            : base(receiver) { }
        public override string ToFullString()
        {
            return "await " + Receiver.ToFullString();
        }
    }
}
