namespace AiGateway_Core.Constants
{
    public static class PiiTokens
    {
        public const string Cpr = "[MASKED_CPR]";
        public const string Email = "[MASKED_EMAIL]";
        public const string ApiKey = "[MASKED_API_KEY]";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            Cpr,
            Email,
            ApiKey
        };
    }
}
