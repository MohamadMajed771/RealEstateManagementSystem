using System.Security.Cryptography;

namespace PropertyTransferManagement.Services
{
    public record StoredDocument(
        string StorageKey, string OriginalFileName, string ContentType, long SizeBytes, string Sha256);

    /// <summary>
    /// Stores case documents OUTSIDE wwwroot (App_Data/case-documents),
    /// so they can only be downloaded through an authorized controller action.
    /// Validates extension, size and file signature (magic bytes).
    /// </summary>
    public class DocumentStorage
    {
        public const long MaxBytes = 10 * 1024 * 1024; // 10 MB

        private static readonly Dictionary<string, (string ContentType, byte[][] Signatures)> Allowed = new()
        {
            [".pdf"] = ("application/pdf", new[] { "%PDF"u8.ToArray() }),
            [".png"] = ("image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } }),
            [".jpg"] = ("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
            [".jpeg"] = ("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } })
        };

        private readonly string _root;

        public DocumentStorage(IWebHostEnvironment env)
        {
            _root = Path.Combine(env.ContentRootPath, "App_Data", "case-documents");
        }

        /// <summary>Returns an error message, or null when the file is acceptable.</summary>
        public static string? Validate(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return "Please choose a file.";

            if (file.Length > MaxBytes)
                return "The file is larger than 10 MB.";

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!Allowed.TryGetValue(ext, out var rule))
                return "Only PDF, JPG and PNG files are accepted.";

            var header = new byte[8];
            using var stream = file.OpenReadStream();
            var read = stream.Read(header, 0, header.Length);

            var ok = rule.Signatures.Any(sig => read >= sig.Length && header.Take(sig.Length).SequenceEqual(sig));
            return ok ? null : "The file content does not match its extension.";
        }

        public async Task<StoredDocument> SaveAsync(Guid caseId, IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var storageKey = Path.Combine(caseId.ToString(), $"{Guid.NewGuid():N}{ext}");
            var fullPath = Path.Combine(_root, storageKey);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using (var target = File.Create(fullPath))
            {
                await file.CopyToAsync(target);
            }

            string hash;
            await using (var saved = File.OpenRead(fullPath))
            {
                hash = Convert.ToHexString(await SHA256.HashDataAsync(saved)).ToLowerInvariant();
            }

            var originalName = Path.GetFileName(file.FileName);
            if (originalName.Length > 255) originalName = originalName[^255..];

            return new StoredDocument(storageKey, originalName, Allowed[ext].ContentType, file.Length, hash);
        }

        public string GetFullPath(string storageKey)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_root, storageKey));

            // Never serve anything outside the storage folder.
            if (!fullPath.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Invalid storage key.");

            return fullPath;
        }
    }
}
