using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Casualties_Hub.Services;

/// <summary>
/// Signs the player into Nexus Mods with OAuth2 + PKCE, so the Hub never sees or stores
/// a personal API key.
/// </summary>
public sealed class NexusAuthService
{
    // TODO: swap for the real client_id once Nexus Mods registers Casualties Hub and
    // replies with one (see the "Re: Username Change and Casualties Hub Verification
    // Inquiry" support thread). Sign-in refuses to start until this is set.
    private const string ClientId = "REPLACE_WITH_NEXUS_CLIENT_ID";
    private const int CallbackPort = 38719;
    private const string AuthorizeUrl = "https://users.nexusmods.com/oauth/authorize";
    private const string TokenUrl = "https://users.nexusmods.com/oauth/token";

    // The published Nexus Mods user-service signing key, from the OAuth2/PKCE guide at
    // https://modding.wiki/en/api/oauth2-guide, used to verify access tokens are genuine.
    private const string NexusPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDhKHxCWOeUy38S3UOBOB11SNd/
        wyL9TVvzxePkEsZb4fEVGp0U5MEcDcJgXUo/fZOYTUFMX7ipvCC7sbsyKpJ0xZ/M
        l5zXMBcI03gu6p1TvG+eL0xEk6X8LD+t+GbzH9EY58bZ8kOLEx4lbAX3fNYhMhbh
        HJra9ZVW2QdgHoDV6wIDAQAB
        -----END PUBLIC KEY-----
        """;

    private static readonly Uri RedirectUri = new($"http://127.0.0.1:{CallbackPort}/callback/");

    private readonly NexusOAuthTokenStore _tokenStore;
    private readonly HttpClient _client = new();

    public NexusAuthService(SettingsService settingsService) => _tokenStore = new(settingsService);

    public bool IsSignedIn => _tokenStore.Load() is not null;
    public string? Username => _tokenStore.Load()?.Username;
    public bool IsPremium => _tokenStore.Load()?.IsPremium ?? false;

    /// <summary>Opens the Nexus sign-in page in the player's browser and waits for the OAuth callback.</summary>
    public async Task<NexusSignInResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (string.Equals(ClientId, "REPLACE_WITH_NEXUS_CLIENT_ID", StringComparison.Ordinal))
            return NexusSignInResult.Failed("Casualties Hub is not yet registered with Nexus Mods for sign-in. This will be enabled in a future update.");

        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = Guid.NewGuid().ToString("N");

        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri.ToString());
        try
        {
            listener.Start();
        }
        catch (HttpListenerException exception)
        {
            return NexusSignInResult.Failed($"Could not start the local sign-in listener: {exception.Message}");
        }

        try
        {
            Process.Start(new ProcessStartInfo(BuildAuthorizeUrl(state, codeChallenge)) { UseShellExecute = true });

            var getContextTask = listener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3), cancellationToken);
            var completed = await Task.WhenAny(getContextTask, timeoutTask);
            if (completed == timeoutTask)
                return NexusSignInResult.Failed("Sign-in timed out waiting for the browser.");

            var context = await getContextTask;
            var query = context.Request.QueryString;
            var code = query["code"];
            var returnedState = query["state"];
            RespondWithClosePage(context, success: !string.IsNullOrWhiteSpace(code) && string.Equals(returnedState, state, StringComparison.Ordinal));

            if (!string.Equals(returnedState, state, StringComparison.Ordinal))
                return NexusSignInResult.Failed("Sign-in response failed state validation.");
            if (string.IsNullOrWhiteSpace(code))
                return NexusSignInResult.Failed(query["error_description"] ?? "Nexus did not return an authorization code.");

            var tokens = await ExchangeCodeAsync(code, codeVerifier, cancellationToken);
            var (username, isPremium) = DecodeAccessToken(tokens.AccessToken);
            _tokenStore.Save(new NexusTokenSet
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn),
                Username = username,
                IsPremium = isPremium
            });
            return NexusSignInResult.Succeeded(username);
        }
        finally
        {
            listener.Stop();
        }
    }

    public void SignOut() => _tokenStore.Clear();

    /// <summary>Returns a valid access token, refreshing it first if needed, or null if signed out.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        var tokens = _tokenStore.Load();
        if (tokens is null) return null;
        if (DateTime.UtcNow < tokens.ExpiresAtUtc - TimeSpan.FromMinutes(1)) return tokens.AccessToken;

        try
        {
            var refreshed = await RefreshAsync(tokens.RefreshToken);
            var (username, isPremium) = DecodeAccessToken(refreshed.AccessToken);
            var updated = new NexusTokenSet
            {
                AccessToken = refreshed.AccessToken,
                RefreshToken = refreshed.RefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn),
                Username = username,
                IsPremium = isPremium
            };
            _tokenStore.Save(updated);
            return updated.AccessToken;
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not refresh the Nexus sign-in", exception);
            _tokenStore.Clear();
            return null;
        }
    }

    private static string BuildAuthorizeUrl(string state, string codeChallenge)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["scope"] = "",
            ["redirect_uri"] = RedirectUri.ToString(),
            ["state"] = state,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = codeChallenge
        };
        var queryString = string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return $"{AuthorizeUrl}?{queryString}";
    }

    private Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken) =>
        PostTokenRequestAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri.ToString(),
            ["client_id"] = ClientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier
        }, cancellationToken);

    private Task<TokenResponse> RefreshAsync(string refreshToken) =>
        PostTokenRequestAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ClientId,
            ["refresh_token"] = refreshToken
        }, CancellationToken.None);

    private async Task<TokenResponse> PostTokenRequestAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = new FormUrlEncodedContent(form) };
        using var response = await _client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nexus sign-in request failed ({(int)response.StatusCode}): {body}");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new TokenResponse(
            root.GetProperty("access_token").GetString()!,
            root.GetProperty("refresh_token").GetString()!,
            root.GetProperty("expires_in").GetInt32());
    }

    private static void RespondWithClosePage(HttpListenerContext context, bool success)
    {
        var message = success ? "Signed in to Casualties Hub. You can close this tab." : "Nexus sign-in did not complete. You can close this tab and try again.";
        var html = $"<html><body style=\"font-family:sans-serif;background:#111;color:#eee;padding:40px;\">{message}</body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
    }

    private static (string? Username, bool IsPremium) DecodeAccessToken(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length != 3)
            throw new InvalidOperationException("Nexus returned a malformed access token.");
        if (!VerifySignature(parts[0], parts[1], parts[2]))
            throw new InvalidOperationException("Nexus access token signature could not be verified.");

        using var document = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        if (!document.RootElement.TryGetProperty("user", out var user))
            return (null, false);

        var username = user.TryGetProperty("username", out var usernameProperty) ? usernameProperty.GetString() : null;
        var isPremium = user.TryGetProperty("membership_roles", out var roles) && roles.ValueKind == JsonValueKind.Array
            && roles.EnumerateArray().Any(role => role.GetString() is "premium" or "lifetimepremium");
        return (username, isPremium);
    }

    private static bool VerifySignature(string headerSegment, string payloadSegment, string signatureSegment)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(NexusPublicKeyPem);
        var data = Encoding.ASCII.GetBytes($"{headerSegment}.{payloadSegment}");
        return rsa.VerifyData(data, Base64UrlDecode(signatureSegment), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static string GenerateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string GenerateCodeChallenge(string verifier) => Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - (padded.Length % 4)) % 4);
        return Convert.FromBase64String(padded);
    }

    private readonly record struct TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
}

public sealed record NexusSignInResult(bool Success, string? Username, string? Error)
{
    public static NexusSignInResult Succeeded(string? username) => new(true, username, null);
    public static NexusSignInResult Failed(string error) => new(false, null, error);
}
