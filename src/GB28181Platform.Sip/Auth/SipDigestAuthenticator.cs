using System.Security.Cryptography;
using System.Text;
using SIPSorcery.SIP;

namespace GB28181Platform.Sip.Auth;

public class SipDigestAuthenticator
{
    private readonly string _realm;

    public SipDigestAuthenticator(string realm)
    {
        _realm = realm;
    }

    /// <summary>
    /// 生成 401 响应，携带 nonce
    /// </summary>
    public SIPResponse CreateUnauthorizedResponse(SIPRequest request)
    {
        var response = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Unauthorised, null);
        var nonce = Guid.NewGuid().ToString("N");
        var authHeader = $"Digest realm=\"{_realm}\", nonce=\"{nonce}\", algorithm=MD5";
        response.Header.AuthenticationHeaders.Add(
            SIPAuthenticationHeader.ParseSIPAuthenticationHeader(
                SIPAuthorisationHeadersEnum.WWWAuthenticate, authHeader));
        return response;
    }

    /// <summary>
    /// 验证 Digest 认证
    /// </summary>
    public bool Authenticate(SIPRequest request, string password)
    {
        if (request.Header.AuthenticationHeaders == null || request.Header.AuthenticationHeaders.Count == 0)
            return false;

        var authHeader = request.Header.AuthenticationHeaders[0];
        var digest = authHeader.SIPDigest;

        var ha1 = Md5Hash($"{digest.Username}:{digest.Realm}:{password}");
        var ha2 = Md5Hash($"{request.Method}:{digest.URI}");
        var expectedResponse = Md5Hash($"{ha1}:{digest.Nonce}:{ha2}");

        return string.Equals(digest.Response, expectedResponse, StringComparison.OrdinalIgnoreCase);
    }

    private static string Md5Hash(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
