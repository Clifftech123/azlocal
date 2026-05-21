namespace AzLocal.Core.Exceptions;

/// <summary>
/// Thrown by service handlers when an Azure API operation fails.
/// The host catches this and writes an Azure-format JSON error response:
///   { "error": { "code": "ResourceNotFound", "message": "..." } }
///
/// Always use the static factory methods rather than the constructor directly —
/// they ensure the correct HTTP status code and Azure error code are paired together.
/// </summary>
public class AzureEmulatorException : Exception
{
    /// <summary>
    /// HTTP status code to return — e.g. 404, 409, 400.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Azure error code string returned in the response body — e.g. "ResourceNotFound".
    /// The Azure SDK uses this string to identify the type of error.
    /// </summary>
    public string ErrorCode { get; }

    public AzureEmulatorException(int statusCode, string errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }



    /// <summary>
    /// 404 — the requested resource does not exist.
    /// Use when a blob, container, secret, queue, or resource group is not found.
    /// </summary>
    public static AzureEmulatorException NotFound(string message) =>
        new(404, "ResourceNotFound", message);

    /// <summary>
    /// 409 — the resource already exists and cannot be created again.
    /// Use when creating a container or queue that already exists.
    /// </summary>
    public static AzureEmulatorException Conflict(string message) =>
        new(409, "ResourceAlreadyExists", message);

    /// <summary>
    /// 400 — the request body or parameters are invalid.
    /// Use when required fields are missing or values are malformed.
    /// </summary>
    public static AzureEmulatorException BadRequest(string message) =>
        new(400, "InvalidRequest", message);

    /// <summary>
    /// 403 — the operation is not permitted.
    /// Use when a disabled secret is accessed or a lease conflict occurs.
    /// </summary>
    public static AzureEmulatorException Forbidden(string message) =>
        new(403, "Forbidden", message);

    /// <summary>
    /// 500 — an unexpected internal error occurred.
    /// </summary>
    public static AzureEmulatorException Internal(string message) =>
        new(500, "InternalError", message);
}
