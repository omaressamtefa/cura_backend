using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AuthApi.Services;

public class ImageService : IImageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageService> _logger;

    public ImageService(IWebHostEnvironment environment, ILogger<ImageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveImageAsync(IFormFile image, string entityType, int entityId, string? existingImageUrl)
    {
        try
        {
            // Validate the image
            if (image == null || image.Length == 0)
            {
                throw new InvalidOperationException("No image provided.");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(image.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Invalid image format. Only JPG, JPEG, and PNG are allowed.");
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", entityType);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            if (!string.IsNullOrEmpty(existingImageUrl))
            {
                DeleteImage(existingImageUrl);
            }

            var fileName = $"{entityType}_{entityId}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            var imageUrl = $"/images/{entityType}/{fileName}";
            _logger.LogInformation("Image saved successfully at {ImageUrl}", imageUrl);
            return imageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save image for {EntityType} ID {EntityId}", entityType, entityId);
            throw new InvalidOperationException("Failed to save image.", ex);
        }
    }

    public void DeleteImage(string imageUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return;
            }

            var fileName = Path.GetFileName(imageUrl);
            var relativePath = imageUrl.StartsWith("/") ? imageUrl.Substring(1) : imageUrl;
            var filePath = Path.Combine(_environment.WebRootPath, relativePath);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Image deleted successfully: {ImageUrl}", imageUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete image: {ImageUrl}", imageUrl);
            throw;
        }
    }
}