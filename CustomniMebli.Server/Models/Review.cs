namespace CustomniMebli.Server.Models;

public class Review
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsVisible { get; set; } = true;
}
