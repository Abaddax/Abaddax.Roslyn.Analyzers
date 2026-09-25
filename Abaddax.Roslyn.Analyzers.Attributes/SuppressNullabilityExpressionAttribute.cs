namespace Abaddax.Roslyn.Analyzers.Attributes
{
    /// <summary>
    /// Suppresses nullability warnings on a <see cref="System.Linq.Expressions.Expression{System.Func}"/> parameter
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class SuppressNullabilityExpressionAttribute : Attribute;
}
