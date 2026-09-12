namespace Abaddax.Roslyn.Analyzers.Attributes
{
    /// <summary>
    /// Indicates that the caller function parameter is forwarded to a specific lambda parameter
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class ForwardedParameterAttribute : Attribute
    {
        public IReadOnlyList<string?> ForwardedParameterNames { get; }

        /// <summary>
        /// Delares the lambda to forward the given <paramref name="forwardedParameterNames"/>.
        /// </summary>
        /// <remarks>Types and parameter count must match the lambda parameters</remarks>
        /// <param name="forwardedParameterNames"><see langword="nameof"/> the caller parameter name. <see langword="null"/> if not forwarded</param>
        public ForwardedParameterAttribute(params string?[] forwardedParameterNames)
        {
            ForwardedParameterNames = forwardedParameterNames;
        }
    }
}
