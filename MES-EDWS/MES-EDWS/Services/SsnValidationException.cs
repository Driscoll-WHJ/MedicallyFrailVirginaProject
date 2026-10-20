using System.Text.RegularExpressions;

namespace MES_EDWS.Services
{
    /// <summary>Thrown when an SSN does not meet the required format XXX-XX-XXXX (e.g. 096-22-4256).</summary>
    public partial class SsnValidationException : Exception
    {
        public const string ErrorCode = "8000";

        public const string RequiredFormatMessage =
            "SSN failed validation requirements: expected format XXX-XX-XXXX";

        public SsnValidationException(string message) : base(message)
        {
        }

        /// <summary>3 digits, dash, 2 digits, dash, 4 digits (ASCII digits only; leading zeros allowed).</summary>
        public static bool IsValidFormat(string ssn) => SsnFormat().IsMatch(ssn);

        /// <summary>The same SSN without dashes, for matching rows stored as 9 digits.</summary>
        public static string WithoutDashes(string ssn) => ssn.Replace("-", string.Empty);

        [GeneratedRegex(@"^[0-9]{3}-[0-9]{2}-[0-9]{4}$", RegexOptions.CultureInvariant)]
        private static partial Regex SsnFormat();
    }
}
