namespace Abaddax.Roslyn.Analyzers.Attributes
{
    /// <summary>
    /// Indicates that the given property is always included by efcore
    /// </summary>
    [AttributeUsage(AttributeTargets.ReturnValue | AttributeTargets.Parameter, AllowMultiple = true)]
    public sealed class EfCorePropertyIncludedAttribute : Attribute
    {
        public string PropertyName { get; }

        /// <summary>
        /// Marks the given <paramref name="propertyName"/> as included by efcore
        /// </summary>
        /// <param name="propertyName"><see langword="nameof"/> the included property. Is the property is a subproperty then <see langword="nameof"/>(A).<see langword="nameof"/>(A.B)</param>
        /// <exception cref="ArgumentNullException"></exception>
        public EfCorePropertyIncludedAttribute(string propertyName)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }
    }
}
