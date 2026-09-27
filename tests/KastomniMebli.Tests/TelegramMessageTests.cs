using KastomniMebli.Web.Data;
using KastomniMebli.Web.Notifications;

namespace KastomniMebli.Tests;

public class TelegramMessageTests
{
    [Fact]
    public void Contains_all_fields_in_kyiv_time()
    {
        var lead = new Lead
        {
            Id = 42,
            CreatedAt = new DateTime(2026, 9, 27, 9, 5, 0, DateTimeKind.Utc),
            Name = "Олена",
            Phone = "+380671234567",
            FurnitureTypes = ["kitchen", "wardrobe"],
            Location = "Звягель",
            Dimensions = "3000 × 2600",
            Comment = "Котел у кутку",
        };

        var text = TelegramMessage.ForLead(lead, TelegramMessage.KyivTime);

        Assert.Contains("#42", text);
        Assert.Contains("Олена", text);
        Assert.Contains("+380 67 123 45 67", text);
        Assert.Contains("Кухня, Шафа-купе", text);
        Assert.Contains("Звягель", text);
        Assert.Contains("3000 × 2600", text);
        Assert.Contains("Котел у кутку", text);
        Assert.Contains("27.09.2026 12:05", text); // UTC+3 (літній час)
    }

    [Fact]
    public void Escapes_html_from_client()
    {
        var lead = new Lead
        {
            CreatedAt = DateTime.UtcNow,
            Name = "<b>Хакер</b> & Ко",
            Phone = "+380671234567",
            Comment = "<a href=\"x\">клік</a>",
        };

        var text = TelegramMessage.ForLead(lead, TimeZoneInfo.Utc);

        Assert.Contains("&lt;b&gt;Хакер&lt;/b&gt; &amp; Ко", text);
        Assert.DoesNotContain("<a href", text);
    }

    [Fact]
    public void Empty_fields_shown_as_dash()
    {
        var lead = new Lead { CreatedAt = DateTime.UtcNow, Name = "Іван", Phone = "+380671234567" };

        var text = TelegramMessage.ForLead(lead, TimeZoneInfo.Utc);

        Assert.Contains("<b>Що потрібно:</b> —", text);
        Assert.Contains("<b>Коментар:</b> —", text);
    }
}
