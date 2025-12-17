using System.IO.Compression;
using System.Text.Json;

namespace GmrProcessor.Utils;

public static class MessageDeserializer
{
    private static readonly JsonSerializerOptions s_defaultSerializerOptions = new(JsonSerializerDefaults.Web);

    public static T? Deserialize<T>(string message, string? contentEncoding)
    {
        if (contentEncoding != null && contentEncoding != "gzip, base64")
        {
            throw new NotImplementedException(
                "Only 'gzip, base64' content encoding is supported, passed: " + contentEncoding
            );
        }

        if (contentEncoding == null)
            return JsonSerializer.Deserialize<T>(message, s_defaultSerializerOptions);

        var compressedBytes = Convert.FromBase64String(message);
        using var compressedStream = new MemoryStream(compressedBytes);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);

        return JsonSerializer.Deserialize<T>(gzipStream, s_defaultSerializerOptions);
    }
}
