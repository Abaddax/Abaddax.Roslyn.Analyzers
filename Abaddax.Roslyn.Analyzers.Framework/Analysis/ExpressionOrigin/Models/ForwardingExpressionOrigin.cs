namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.ExpressionOrigin.Models
{
    public abstract class ForwardingExpressionOrigin : ExpressionOrigin
    {
        public ExpressionOrigin Receiver { get; }
        protected ForwardingExpressionOrigin(ExpressionOrigin receiver)
        {
            Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        }
    }
}
