namespace GmrProcessor.Processors.ImportGmrMatching;

public class ImportMatchMessage
{
    public required string ImportReference { get; set; }

    public required bool Match { get; set; }
}
