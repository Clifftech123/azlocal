namespace AzLocal.Core.Exceptions;

public class AzureEmulatorException : Exception
{
    public int StatusCode { get; }

    public AzureEmulatorException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
