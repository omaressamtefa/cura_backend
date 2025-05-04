namespace AuthApi.Services;

public interface IImageUploadService
{
    Task<string?> UploadImageAsync(IFormFile image, string prefix, int id);
}