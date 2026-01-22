namespace GmrProcessor.Config;

public class FeatureOptions
{
    [ConfigurationKeyName("ENABLE_DEV_ENDPOINTS")]
    public bool EnableDevEndpoints { get; init; } = false;

    [ConfigurationKeyName("DEV_ENDPOINT_USERNAME")]
    public string? DevEndpointUsername { get; init; }

    [ConfigurationKeyName("DEV_ENDPOINT_PASSWORD")]
    public string? DevEndpointPassword { get; init; }

    [ConfigurationKeyName("ENABLE_TRADE_IMPORTS_MESSAGING")]
    public bool EnableTradeImportsMessaging { get; init; } = false;

    [ConfigurationKeyName("ENABLE_STORE_OUTBOUND_MESSAGES")]
    public bool EnableStoreOutboundMessages { get; init; } = false;

    [ConfigurationKeyName("ENABLE_SQS_CONSUMERS")]
    public bool EnableSqsConsumers { get; init; } = false;

    [ConfigurationKeyName("ENABLE_GVMS_API_CLIENT_HOLD")]
    public bool EnableGvmsApiClientHold { get; init; } = false;

    [ConfigurationKeyName("ENABLE_GVMS_API_CLIENT_IGNORE_NOT_FOUND")]
    public bool EnableGvmsApiClientIgnoreNotFound { get; init; }
}
