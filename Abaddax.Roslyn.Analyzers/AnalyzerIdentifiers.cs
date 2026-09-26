using System.Globalization;
using System.Reflection;

namespace Abaddax.Roslyn.Analyzers
{
    internal static class AnalyzerIdentifiers
    {
        #region Analyzers

        public const string AddCancellationTokenAnalyzer = "ABX0001";
        public const string PreferAsyncSuffixAnalyzer = "ABX0002";
        public const string PreferAsyncOverloadAnalyzer = "ABX0003";
        public const string EfCorePreferAsyncCallAnalyzer = "ABX0004";
        public const string EfCoreExplicitTrackingAnalyzer = "ABX0005";
        public const string UnconditionalSelfRecursionAnalyzer = "ABX0006";
        public const string EfCoreThenIncludeFormattingAnalyzer = "ABX0007";
        public const string SuppressionJustificationAnalyzer = "ABX0008";

        #endregion

        #region Suppression

        public const string EfCoreDereferencePossibleNullReferenceSuppression = "ABX1001";
        public const string EfCoreQueryNullReferenceSuppression = "ABX1002";
        public const string UnusedExceptionAssignmentSuppression = "ABX1003";
        public const string UnusedCancellationTokenParameterSuppression = "ABX1004";
        public const string ProtectedReadonlyFieldSuppression = "ABX1005";

        #endregion

        public static string GetAnalyzerHelpUri(string identifier)
        {
            var version = typeof(AnalyzerIdentifiers).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return string.IsNullOrEmpty(version)
                ? string.Format(CultureInfo.InvariantCulture, "https://github.com/Abaddax/Abaddax.Roslyn.Analyzers/blob/master/docs/Analyzers/{0}.md", identifier)
                : string.Format(CultureInfo.InvariantCulture, "https://github.com/Abaddax/Abaddax.Roslyn.Analyzers/blob/v{0}/docs/Analyzers/{1}.md", version, identifier);
        }
    }
}
