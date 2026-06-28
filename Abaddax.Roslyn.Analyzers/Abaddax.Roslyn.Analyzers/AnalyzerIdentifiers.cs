namespace Abaddax.Roslyn.Analyzers
{
    internal static class AnalyzerIdentifiers
    {
        #region Analyzers

        public const string AddCancellationTokenAnalyzer = "ABX0001";
        public const string PreferAsyncSuffixAnalyzer = "ABX0002";
        public const string PreferAsyncOverloadAnalyzer = "ABX0003";
        public const string EfCorePreferAsyncCallAnalyzer = "ABX0004";

        #endregion

        #region Suppression

        public const string EfCoreDereferencePossibleNullReferenceSuppression = "ABX1001";
        public const string EfCoreQueryNullReferenceSuppression = "ABX1002";
        public const string UnusedExceptionAssignmentSuppression = "ABX1003";

        #endregion
    }
}
