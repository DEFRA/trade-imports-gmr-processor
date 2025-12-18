namespace GmrProcessor.Config;

public class FeatureOptions
{
    [ConfigurationKeyName("ENABLE_TRADE_IMPORTS_MESSAGING")]
    public bool EnableTradeImportsMessaging { get; init; } = false;
}
