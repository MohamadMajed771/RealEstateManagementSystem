
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace RealEstateMarketplace.Services
{
    public class PropertyPhotoService
    {
        private const long MaxFileSize = 5 * 1024 * 1024;

        private readonly string _uploadRoot;
        private readonly string _uploadDirectory;

        public PropertyPhotoService(IWebHostEnvironment environment)
        {
            _uploadRoot = Path.Combine(environment.WebRootPath, "uploads");
            _uploadDirectory = Path.Combine(_uploadRoot, "properties");

            Directory.CreateDirectory(_uploadDirectory);
        }

        /// <summary>Profile pictures use the same checks, in uploads/profiles.</summary>
        public Task<string> UploadProfilePictureAsync(IFormFile file) => UploadPhotoAsync(file, "profiles");

        public Task DeleteProfilePictureAsync(string? imagePath) =>
            DeleteFromAsync(Path.Combine(_uploadRoot, "profiles"), imagePath);

        public async Task<string> UploadPhotoAsync(IFormFile file, string folder = "properties")
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Please select a photo.");

            if (file.Length > MaxFileSize)
                throw new ArgumentException(
                    "Each photo must be 5 MB or smaller.");

            var extension = Path.GetExtension(file.FileName)
                .ToLowerInvariant();

            var allowedExtensions = new[]
            {
                ".jpg", ".jpeg", ".png", ".webp"
            };

            if (!allowedExtensions.Contains(extension))
                throw new ArgumentException(
                    "Only JPG, JPEG, PNG and WebP photos are allowed.");

            var header = new byte[12];

            await using (var stream = file.OpenReadStream())
            {
                var bytesRead = 0;

                while (bytesRead < header.Length)
                {
                    var count = await stream.ReadAsync(
                        header.AsMemory(bytesRead));

                    if (count == 0)
                        break;

                    bytesRead += count;
                }

                if (bytesRead < 12 || !IsValidSignature(header, extension))
                    throw new ArgumentException(
                        "The selected file is not a valid image format.");
            }

            var directory = Path.Combine(_uploadRoot, folder);
            Directory.CreateDirectory(directory);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(directory, fileName);

            await using (var output = new FileStream(
                fullPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(output);
            }

            return $"/uploads/{folder}/{fileName}";
        }

        public Task DeletePhotoAsync(string imagePath) => DeleteFromAsync(_uploadDirectory, imagePath);

        private static Task DeleteFromAsync(string directory, string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return Task.CompletedTask;

            var fileName = Path.GetFileName(imagePath);

            if (string.IsNullOrWhiteSpace(fileName))
                return Task.CompletedTask;

            var fullPath = Path.Combine(directory, fileName);

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return Task.CompletedTask;
        }

        private static bool IsValidSignature(
            byte[] header, string extension)
        {
            if (extension is ".jpg" or ".jpeg")
            {
                return header[0] == 0xFF &&
                       header[1] == 0xD8 &&
                       header[2] == 0xFF;
            }

            if (extension == ".png")
            {
                byte[] pngSignature =
                {
                    0x89, 0x50, 0x4E, 0x47,
                    0x0D, 0x0A, 0x1A, 0x0A
                };

                return header.Take(8).SequenceEqual(pngSignature);
            }

            if (extension == ".webp")
            {
                return System.Text.Encoding.ASCII
                           .GetString(header, 0, 4) == "RIFF"
                    && System.Text.Encoding.ASCII
                           .GetString(header, 8, 4) == "WEBP";
            }

            return false;
        }
    }
}
