using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ImageGallery.Client.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class IdentityController : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<string>> GetIdentity()
        {
            var identityToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.IdToken);
            return Ok(identityToken);

        }

        [HttpGet()]
        [Route("AccessToken")]

        public async Task<ActionResult<string>> GetAccessToken()
        {
            var accessToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);
            return Ok(accessToken);
        }

        [HttpGet]
        [Route("claims")]
        public ActionResult<Dictionary<string, string>> GetClaims()
        {
            var claims = new Dictionary<string, string>();
            foreach (var claim in User.Claims)
            {
                claims.Add(claim.Type, claim.Value);
            }
            return Ok(claims);

        }
    }
}