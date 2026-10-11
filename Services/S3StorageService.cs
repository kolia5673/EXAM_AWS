
using Amazon.S3;
using Amazon.S3.Model;

namespace AIImageService.Services;

public class S3StorageService
{
    private readonly IAmazonS3 _s3;
    private readonly IConfiguration _configuration;

    public S3StorageService(
        IAmazonS3 s3,
        IConfiguration configuration)
    {
        _s3 = s3;
        _configuration = configuration;
    }

    public async Task<string> UploadAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var bucketName = _configuration["AWS:BucketName"]
            ?? throw new InvalidOperationException("S3 bucket is not configured.");

        var key = $"images/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";

        using var stream = new MemoryStream(imageBytes);

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        };

        await _s3.PutObjectAsync(request, cancellationToken);

        return key;
    }
}
