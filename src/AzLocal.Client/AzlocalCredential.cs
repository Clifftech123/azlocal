using AzLocal.Core;
using Azure.Core;

namespace AzLocal.Client;

/// <summary>
/// An <see cref="TokenCredential"/> that returns the static fake token vended by
/// <c>ImdsMiddleware</c>. Pass this to any Azure SDK client constructor to authenticate
/// against the local AzLocal emulator without needing a real Azure AD tenant.
/// </summary>
public sealed class AzlocalCredential : TokenCredential
{
    // Token value is shared with ImdsMiddleware via EmulatorDefaults — they must always match.
    public static readonly string TokenValue = EmulatorDefaults.FakeToken;

    private static readonly AccessToken Token = new(TokenValue, DateTimeOffset.MaxValue);

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => Token;

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => ValueTask.FromResult(Token);
}
