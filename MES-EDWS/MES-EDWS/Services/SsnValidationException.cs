using System.Text.RegularExpressions;

namespace MES_EDWS.Services
{
    /// <summary>Thrown when an SSN is not exactly 9 digits with no dashes (e.g. 096224256).</summary>
    public partial class SsnValidationException : Exception
    {
        public const string ErrorCode = "8000";

        public const string RequiredFormatMessage =
            "SSN failed validation requirements: expected 9 digits with no dashes";

        public SsnValidationException(string message) : base(message)
        {
        }

        /// <summary>Exactly 9 ASCII digits; leading zeros allowed.</summary>
        public static bool IsValidFormat(string ssn) => SsnFormat().IsMatch(ssn);

        [GeneratedRegex(@"^[0-9]{9}$", RegexOptions.CultureInvariant)]
        private static partial Regex SsnFormat();
    }
}
