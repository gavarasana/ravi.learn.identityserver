using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageGallery.Client.Dtos;
using ImageGallery.Client.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ImageGallery.Client.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<int>> UploadFile([FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            //var formFile = Request.Form.Files[0];
            if (file== null)
            {
                return BadRequest();
            }

            if (file.ContentType == "application/pdf")
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream, cancellationToken);
                if (memoryStream.Length < 262144)
                {
                    _ = new AppFile
                    {
                        Content = memoryStream.ToArray()
                    };
                    return Ok(12134);
                }
                return BadRequest();
            }
            return BadRequest();
        }
    }
}