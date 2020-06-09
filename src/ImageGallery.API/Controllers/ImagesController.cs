using AutoMapper;
using ImageGallery.API.Services;
using ImageGallery.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ImageGallery.API.Controllers
{
    [Route("api/images")]
    [ApiController]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        private readonly IGalleryRepository _galleryRepository;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<ImagesController> _logger;
        private readonly IMapper _mapper;

        public ImagesController(
            IGalleryRepository galleryRepository,
            IWebHostEnvironment hostingEnvironment,
            ILogger<ImagesController> logger,
            IMapper mapper)
        {
            _galleryRepository = galleryRepository ?? 
                throw new ArgumentNullException(nameof(galleryRepository));
            _hostingEnvironment = hostingEnvironment ?? 
                throw new ArgumentNullException(nameof(hostingEnvironment));
            _logger = logger;
            _mapper = mapper ?? 
                throw new ArgumentNullException(nameof(mapper));
        }

        [HttpGet()]
        public async Task<IActionResult> GetImagesAsync()
        {
            var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            // get from repo
            var imagesFromRepo = await _galleryRepository.GetImagesAsync(subjectId);

            // map to model
            var imagesToReturn = _mapper.Map<IEnumerable<Model.Image>>(imagesFromRepo);

            // return
            return Ok(imagesToReturn);
        }

        [HttpGet("{id}", Name = "GetImage")]
        [Authorize("MustOwnImage")]
        public async Task<ActionResult<Model.Image>> GetImageAsync(Guid id)
        {
            var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            var imageFromRepo = await _galleryRepository.GetImageAsync(id, subjectId);

            if (imageFromRepo == null)
            {
                return NotFound();
            }

            var imageToReturn = _mapper.Map<Model.Image>(imageFromRepo);

            return Ok(imageToReturn);
        }

        [HttpPost()]
        [Authorize(Roles = "PaidUser")]
        public async Task<IActionResult> CreateImageAsync([FromBody] ImageForCreation imageForCreation)
        {
            // Automapper maps only the Title in our configuration
            var imageEntity = _mapper.Map<Entities.Image>(imageForCreation);

            // Create an image from the passed-in bytes (Base64), and 
            // set the filename on the image

            // get this environment's web root path (the path
            // from which static content, like an image, is served)
            var webRootPath = _hostingEnvironment.WebRootPath;

            // create the filename
            string fileName = Guid.NewGuid().ToString() + ".jpg";
            
            // the full file path
            var filePath = Path.Combine($"{webRootPath}/images/{fileName}");

            // write bytes and auto-close stream
            System.IO.File.WriteAllBytes(filePath, imageForCreation.Bytes);

            // fill out the filename
            imageEntity.FileName = fileName;

            // ownerId should be set - can't save image in starter solution, will
            // be fixed during the course
            var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            imageEntity.OwnerId = subjectId;

            // add and save.  
            _galleryRepository.AddImage(imageEntity);

            await _galleryRepository.SaveAsync();

            var imageToReturn = _mapper.Map<Image>(imageEntity);

            return CreatedAtRoute("GetImage",
                new { id = imageToReturn.Id },
                imageToReturn);
        }

        [HttpDelete("{id}")]
        [Authorize("MustOwnImage")]
        //[Authorize(Roles = "PaidUser")]
        public async Task<IActionResult> DeleteImage(Guid id)
        {
             var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            var imageFromRepo = await _galleryRepository.GetImageAsync(id, subjectId);

            if (imageFromRepo == null)
            {
                return NotFound();
            }

            _galleryRepository.DeleteImage(imageFromRepo);

            await _galleryRepository.SaveAsync();

            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize("MustOwnImage")]
     //   [Authorize(Roles = "PaidUser")]
        public async Task<IActionResult> UpdateImageAsync(Guid id, [FromBody] ImageForUpdate imageForUpdate)
        {
            _logger.LogDebug($"Access Token:{HttpContext.GetTokenAsync("access_token")}");
            var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            var imageFromRepo = await _galleryRepository.GetImageAsync(id, subjectId);
            if (imageFromRepo == null)
            {
                return NotFound();
            }

            _mapper.Map(imageForUpdate, imageFromRepo);

            _galleryRepository.UpdateImage(imageFromRepo);

            await _galleryRepository.SaveAsync();

            return NoContent();
        }
    }
}