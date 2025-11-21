namespace GmrProcessor.Config;

public class MongoConfig
{
    public const string SectionName = "Mongo";
    public string DatabaseUri { get; init; } = default!;
    public string DatabaseName { get; init; } = default!;
}
