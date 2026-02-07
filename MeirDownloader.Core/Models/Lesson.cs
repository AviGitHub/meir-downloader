namespace MeirDownloader.Core.Models;

public class Lesson
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RabbiName { get; set; } = string.Empty;
    public string SeriesName { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public int Duration { get; set; }
}
