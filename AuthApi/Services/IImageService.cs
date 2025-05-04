using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AuthApi.Services;

public interface IImageService
{
    Task<string> SaveImageAsync(IFormFile image, string entityType, int entityId, string? existingImageUrl);
    void DeleteImage(string imageUrl);
}