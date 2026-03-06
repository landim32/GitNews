namespace GitNews.DTO;

public class ProcessingResultInfo
{
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public int TotalCount { get; set; }
    public bool HasErrors => ErrorCount > 0;
}
