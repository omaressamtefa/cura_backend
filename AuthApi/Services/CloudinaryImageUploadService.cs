using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace AuthApi.Services;

public class CloudinaryImageUploadService : IImageUploadService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageUploadService> _logger;

    public CloudinaryImageUploadService(IConfiguration configuration, ILogger<CloudinaryImageUploadService> logger)
    {
        _logger = logger;
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            throw new ArgumentException("Cloudinary configuration is missing.");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string?> UploadImageAsync(IFormFile image, string prefix, int id)
    {
        if (image == null || image.Length == 0)
        {
            _logger.LogInformation("No image provided for {Prefix} with ID {Id}", prefix, id);
            return null;
        }

        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        if (image.Length > maxFileSize)
        {
            _logger.LogWarning("File size exceeds 5MB for {Prefix} with ID {Id}. Size: {FileSize} bytes", prefix, id, image.Length);
            throw new InvalidOperationException("File size exceeds the maximum limit of 5MB.");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var fileExtension = Path.GetExtension(image.FileName).ToLower();
        if (!allowedExtensions.Contains(fileExtension))
        {
            _logger.LogWarning("Invalid file type for {Prefix} with ID {Id}. Extension: {Extension}", prefix, id, fileExtension);
            throw new InvalidOperationException("Invalid file type. Only JPG, JPEG, PNG, and GIF are allowed.");
        }

        try
        {
            using var stream = image.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(image.FileName, stream),
                PublicId = $"{prefix}-{id}-{DateTime.Now:yyyyMMddHHmmss}"
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.LogError("Failed to upload image to Cloudinary for {Prefix} with ID {Id}. Status: {Status}", prefix, id, uploadResult.StatusCode);
                throw new InvalidOperationException("Failed to upload image to Cloudinary.");
            }

            _logger.LogInformation("Image uploaded successfully to Cloudinary for {Prefix} with ID {Id}. URL: {Url}", prefix, id, uploadResult.SecureUrl);
            return uploadResult.SecureUrl.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to Cloudinary for {Prefix} with ID {Id}", prefix, id);
            throw new InvalidOperationException("Failed to upload the image. Please try again.", ex);
        }
    }
}