namespace GmrProcessor.Domain.Eta;

public class IpaffsUpdatedTimeOfArrivalMessage
{
    public required string ReferenceNumber { get; init; }
    public required string Mrn { get; init; }
    public required DateTime LocalDateTimeOfArrival { get; init; }
}
