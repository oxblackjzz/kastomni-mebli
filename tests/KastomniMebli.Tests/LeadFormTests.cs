using KastomniMebli.Web.Leads;

namespace KastomniMebli.Tests;

public class LeadFormTests
{
    private static LeadForm Valid() => new()
    {
        Name = "Олена",
        Phone = "067 123 45 67",
        Types = ["kitchen"],
    };

    [Fact]
    public void Valid_form_passes_and_normalizes_phone()
    {
        var result = Valid().Validate();

        Assert.True(result.IsValid);
        Assert.Equal("Олена", result.Lead!.Name);
        Assert.Equal("+380671234567", result.Lead.Phone);
        Assert.Equal("067 123 45 67", result.Lead.PhoneRaw);
        Assert.Equal(["kitchen"], result.Lead.Types);
    }

    [Fact]
    public void Only_name_and_phone_are_required()
    {
        var result = new LeadForm { Name = "Іван", Phone = "0501234567" }.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Lead!.Types);
        Assert.Null(result.Lead.Location);
        Assert.Null(result.Lead.Dimensions);
        Assert.Null(result.Lead.Comment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_is_required(string? name)
    {
        var result = new LeadForm { Name = name, Phone = "0671234567" }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("name", result.Errors.Keys);
    }

    [Fact]
    public void Too_long_name_is_rejected()
    {
        var result = new LeadForm { Name = new string('а', LeadForm.NameMax + 1), Phone = "0671234567" }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("name", result.Errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("+48 512 345 678")]
    public void Phone_must_be_ukrainian(string? phone)
    {
        var result = new LeadForm { Name = "Іван", Phone = phone }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("phone", result.Errors.Keys);
    }

    [Fact]
    public void Reports_all_errors_at_once()
    {
        var result = new LeadForm { Name = "", Phone = "1", Comment = new string('x', LeadForm.CommentMax + 1) }.Validate();

        Assert.Equal(["comment", "name", "phone"], result.Errors.Keys.Order());
    }

    [Theory]
    [InlineData(nameof(LeadForm.Location), LeadForm.LocationMax)]
    [InlineData(nameof(LeadForm.Dimensions), LeadForm.DimensionsMax)]
    [InlineData(nameof(LeadForm.Comment), LeadForm.CommentMax)]
    public void Optional_fields_have_length_limits(string field, int max)
    {
        var tooLong = new string('x', max + 1);
        var form = field switch
        {
            nameof(LeadForm.Location) => new LeadForm { Name = "Іван", Phone = "0671234567", Location = tooLong },
            nameof(LeadForm.Dimensions) => new LeadForm { Name = "Іван", Phone = "0671234567", Dimensions = tooLong },
            _ => new LeadForm { Name = "Іван", Phone = "0671234567", Comment = tooLong },
        };

        var result = form.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(field.ToLowerInvariant(), result.Errors.Keys);
    }

    [Fact]
    public void Unknown_and_duplicate_types_are_dropped()
    {
        var form = new LeadForm { Name = "Іван", Phone = "0671234567", Types = ["kitchen", "<script>", "kitchen", " wardrobe "] };

        Assert.Equal(["kitchen", "wardrobe"], form.Validate().Lead!.Types);
    }

    [Fact]
    public void Text_is_trimmed_and_whitespace_collapsed()
    {
        var form = new LeadForm
        {
            Name = "  Олена \t  Петрівна ",
            Phone = "0671234567",
            Location = "  Звягель,\n вул. Шевченка ",
            Comment = "рядок 1\r\n\r\n\r\n\r\nрядок 2\u0000",
        };

        var lead = form.Validate().Lead!;

        Assert.Equal("Олена Петрівна", lead.Name);
        Assert.Equal("Звягель, вул. Шевченка", lead.Location);
        Assert.Equal("рядок 1\n\nрядок 2", lead.Comment);
    }

    [Fact]
    public void Blank_optional_fields_become_null()
    {
        var lead = new LeadForm { Name = "Іван", Phone = "0671234567", Location = "  ", Comment = "" }.Validate().Lead!;

        Assert.Null(lead.Location);
        Assert.Null(lead.Comment);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("http://spam.example", true)]
    public void Honeypot_detects_bots(string? honeypot, bool isBot)
    {
        Assert.Equal(isBot, new LeadForm { Honeypot = honeypot }.IsBot);
    }
}
