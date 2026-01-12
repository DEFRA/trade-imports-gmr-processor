namespace GmrProcessor.Config;

public class FeatureOptions
{
    [ConfigurationKeyName("ENABLE_TRADE_IMPORTS_MESSAGING")]
    public bool EnableTradeImportsMessaging { get; init; } = false;

    [ConfigurationKeyName("ENABLE_STORE_OUTBOUND_MESSAGES")]
    public bool EnableStoreOutboundMessages { get; init; } = false;

    [ConfigurationKeyName("ENABLE_SQS_CONSUMERS")]
    public bool EnableSqsConsumers { get; init; } = false;
}
