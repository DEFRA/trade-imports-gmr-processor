using System.Diagnostics.CodeAnalysis;
using Amazon.SQS.Model;

namespace GmrProcessor.Extensions;

[ExcludeFromCodeCoverage]
public static class SqsMessageHeaders
{
    public const string ContentEncoding = "Content-Encoding";
    public const string ResourceType = nameof(ResourceType);
    public const string SubResourceType = nameof(SubResourceType);
    public const string ResourceId = nameof(ResourceId);
}

public static class SqsMessageExtensions
{
    public static string? GetResourceType(this Message message) =>
        GetAttributeValue(message, SqsMessageHeaders.ResourceType);

    public static string? GetContentEncoding(this Message message) =>
        GetAttributeValue(message, SqsMessageHeaders.ContentEncoding);

    public static string? GetSubResourceType(this Message message) =>
        GetAttributeValue(message, SqsMessageHeaders.SubResourceType);

    public static string? GetResourceId(this Message message) =>
        GetAttributeValue(message, SqsMessageHeaders.ResourceId);

    private static string? GetAttributeValue(Message message, string key)
    {
        if (message.MessageAttributes is not null && message.MessageAttributes.TryGetValue(key, out var attributeValue))
        {
            return attributeValue?.StringValue;
        }

        return null;
    }
}
