using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGallery.Client.HttpHandlers
{
    public class BearerTokenHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _httpClientFactory;

        public BearerTokenHandler(IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentException(nameof(httpClientFactory));

        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessToken = await GetAccessTokenAsync(cancellationToken);

            if (accessToken != null)
            {
                request.SetBearerToken(accessToken);
            }
            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var expiresAt = await _httpContextAccessor.HttpContext.GetTokenAsync("expires_at");
            var expiresAtDateTimeoffset = DateTimeOffset.Parse(expiresAt, CultureInfo.InvariantCulture);
            if (expiresAtDateTimeoffset.AddSeconds(-60).ToUniversalTime() > DateTime.UtcNow)
            {
                return await _httpContextAccessor.HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);
            }

            var idpHttpClient = _httpClientFactory.CreateClient("IDPClient");

            var discoveryDocumentResponse = await idpHttpClient.GetDiscoveryDocumentAsync(cancellationToken: cancellationToken);

            var refreshToken = await _httpContextAccessor.HttpContext.GetTokenAsync(OpenIdConnectParameterNames.RefreshToken);

            var refreshResponse = await idpHttpClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                RefreshToken = refreshToken,
                Address = discoveryDocumentResponse.TokenEndpoint,
                ClientId = "imagegalleryclient",
                ClientSecret = "D7B60E4F-1924-462E-9DA4-A6A18CD997ED"
            }, cancellationToken);

            if (refreshResponse.IsError)
            {
                return null;
            }

            var newlyMintedTokens = new List<AuthenticationToken>
            {
                new AuthenticationToken
                {
                Name = OpenIdConnectParameterNames.AccessToken,
                Value = refreshResponse.AccessToken
                },
                new AuthenticationToken
                {
                    Name = OpenIdConnectParameterNames.RefreshToken,
                    Value = refreshResponse.RefreshToken
                },
                new AuthenticationToken
                {
                    Name = OpenIdConnectParameterNames.IdToken,
                    Value = refreshResponse.IdentityToken
                },
                new AuthenticationToken
                {
                    Name = "expires_at",
                    Value = (DateTime.UtcNow + TimeSpan.FromSeconds(refreshResponse.ExpiresIn)).ToString("o", CultureInfo.InvariantCulture)
                }
            };

            var currentAuthenticationResult = await _httpContextAccessor.HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            currentAuthenticationResult.Properties.StoreTokens(newlyMintedTokens);

            await Task.Delay(3000);

            await _httpContextAccessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    currentAuthenticationResult.Principal, currentAuthenticationResult.Properties);

            return refreshResponse.AccessToken;
        }
    }
}



