namespace Abaddax.Roslyn.Analyzers.Attributes
{
    /// <summary>
    /// Indicates that the given property is always included by EFCore
    /// </summary>
    [AttributeUsage(AttributeTargets.ReturnValue | AttributeTargets.Parameter, AllowMultiple = true)]
    public sealed class EfCorePropertyIncludedAttribute : Attribute
    {
        public string PropertyName { get; }

        /// <summary>
        /// Marks the given <paramref name="propertyName"/> as included by EFCore
        /// </summary>
        /// <param name="propertyName"><see langword="nameof"/> the included property. Is the property is a sub-property then <see langword="nameof"/>(A).<see langword="nameof"/>(A.B)</param>
        /// <exception cref="ArgumentNullException"></exception>
        public EfCorePropertyIncludedAttribute(string propertyName)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }
    }
}
