using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ImageGallery.Client.Dtos
{
    public class FileDto
    {
        //[Required]
        //[MaxLength(250000)]
        [Display(Name ="File")]
        public IFormFile FormFile { get; set; }
    }
}
