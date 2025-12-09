namespace GmrProcessor.Config;

public abstract class QueueOptions
{
    public required string QueueName { get; init; }
    public int WaitTimeSeconds { get; init; } = 20;
}
