namespace MES_EDWS.Services
{
    /// <summary>Thrown when an SSN does not meet the required format (9 numeric digits, no dashes).</summary>
    public class SsnValidationException : Exception
    {
        public const string ErrorCode = "8000";

        public SsnValidationException(string message) : base(message)
        {
        }
    }
}
