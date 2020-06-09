using IdentityModel.Client;
using ImageGallery.Client.ViewModels;
using ImageGallery.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGallery.Client.Controllers
{

    public class GalleryController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public GalleryController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ??
                throw new ArgumentNullException(nameof(httpClientFactory));
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var httpClient = _httpClientFactory.CreateClient("APIClient");

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/images/");

            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {

                using (var responseStream = await response.Content.ReadAsStreamAsync())
                {
                    return View(new GalleryIndexViewModel(
                        await JsonSerializer.DeserializeAsync<List<Image>>(responseStream)));
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            throw new Exception("Error occurred while accessing API");
        }

        //[Authorize(Roles = "PaidUser")]
        public async Task<IActionResult> EditImage(Guid id)
        {

            var httpClient = _httpClientFactory.CreateClient("APIClient");

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/images/{id}");

            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            using (var responseStream = await response.Content.ReadAsStreamAsync())
            {
                var deserializedImage = await JsonSerializer.DeserializeAsync<Image>(responseStream);

                var editImageViewModel = new EditImageViewModel()
                {
                    Id = deserializedImage.Id,
                    Title = deserializedImage.Title
                };

                return View(editImageViewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // [Authorize(Roles = "PaidUser")]
        public async Task<IActionResult> EditImage(EditImageViewModel editImageViewModel)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            // create an ImageForUpdate instance
            var imageForUpdate = new ImageForUpdate()
            {
                Title = editImageViewModel.Title
            };

            // serialize it
            var serializedImageForUpdate = JsonSerializer.Serialize(imageForUpdate);

            var httpClient = _httpClientFactory.CreateClient("APIClient");

            var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/images/{editImageViewModel.Id}");

            request.Content = new StringContent(
                serializedImageForUpdate,
                System.Text.Encoding.Unicode,
                "application/json");

            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return RedirectToAction("Index");
        }

        //[Authorize(Roles = "PaidUser")]
        public async Task<IActionResult> DeleteImage(Guid id)
        {
            var httpClient = _httpClientFactory.CreateClient("APIClient");

            var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/images/{id}");

            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return RedirectToAction("Index");
        }

        [Authorize(Roles = "PaidUser")]
        public IActionResult AddImage()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "PaidUser")]
        public async Task<IActionResult> AddImage(AddImageViewModel addImageViewModel)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            // create an ImageForCreation instance
            var imageForCreation = new ImageForCreation()
            { Title = addImageViewModel.Title };

            // take the first (only) file in the Files list
            var imageFile = addImageViewModel.Files.First();

            if (imageFile.Length > 0)
            {
                using (var fileStream = imageFile.OpenReadStream())
                using (var ms = new MemoryStream())
                {
                    fileStream.CopyTo(ms);
                    imageForCreation.Bytes = ms.ToArray();
                }
            }

            // serialize it
            var serializedImageForCreation = JsonSerializer.Serialize(imageForCreation);

            var httpClient = _httpClientFactory.CreateClient("APIClient");

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/images");

            request.Content = new StringContent(
                serializedImageForCreation,
                System.Text.Encoding.Unicode,
                "application/json");

            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return RedirectToAction("Index");
        }

        public async Task Logout(CancellationToken cancellationToken)
        {

            var idpClient = _httpClientFactory.CreateClient("IDPClient");
            var discoveryDocumentResponse = await idpClient.GetDiscoveryDocumentAsync();

            if (discoveryDocumentResponse.IsError)
            {
                throw new Exception("Error occurred when retrieving meta data");
            }

            var accessTokenRevokeActionResponse = await idpClient.RevokeTokenAsync(new TokenRevocationRequest
            {
                Address = discoveryDocumentResponse.RevocationEndpoint,
                ClientId = "imagegalleryclient",
                ClientSecret = "D7B60E4F-1924-462E-9DA4-A6A18CD997ED",
                Token = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken)

            }, cancellationToken);

            if (accessTokenRevokeActionResponse.IsError)
            {
                throw new Exception("Error occurred when revoking reference token");
            }

            var refreshTokenRevokeActionResponse = await idpClient.RevokeTokenAsync(new TokenRevocationRequest
            {
                Address = discoveryDocumentResponse.RevocationEndpoint,
                ClientId = "imagegalleryclient",
                ClientSecret = "D7B60E4F-1924-462E-9DA4-A6A18CD997ED",
                Token = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.RefreshToken)

            }, cancellationToken);

            if (accessTokenRevokeActionResponse.IsError)
            {
                throw new Exception("Error occurred when revoking reference token");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);

        }

        //[Authorize(Roles = "PaidUser")]
        [Authorize(Policy = "OrderFrame")]
        public async Task<ViewResult> OrderFrame()
        {
            var httpClient = _httpClientFactory.CreateClient("IDPClient");
            var metaDataResponse = await httpClient.GetDiscoveryDocumentAsync();
            if (metaDataResponse.IsError)
            {
                return View("Error", new ErrorViewModel { RequestId = this.HttpContext.TraceIdentifier, Message = "Unable to retrieve metadata" });
            }

            var accessToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);
            var userInfoResponse = await httpClient.GetUserInfoAsync(new UserInfoRequest { Address = metaDataResponse.UserInfoEndpoint, Token = accessToken });
            if (userInfoResponse.IsError)
            {
                return View("Error", new ErrorViewModel { RequestId = this.HttpContext.TraceIdentifier, Message = "Unable to retrieve user info" });
            }

            var userAddress = userInfoResponse.Claims.FirstOrDefault(c => c.Type == "address")?.Value;

            return View(new OrderFrameViewModel(userAddress));
        }
    }
}
