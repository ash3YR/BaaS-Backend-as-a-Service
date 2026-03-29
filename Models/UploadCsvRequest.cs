using Microsoft.AspNetCore.Http;

namespace BaaS.Models;

public class UploadCsvRequest
{
    public IFormFile? File { get; set; }
}
