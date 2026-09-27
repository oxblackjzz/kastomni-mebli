using KastomniMebli.Web.Leads;

namespace KastomniMebli.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("0671234567", "+380671234567")]
    [InlineData("067 123 45 67", "+380671234567")]
    [InlineData("067-123-45-67", "+380671234567")]
    [InlineData("(067) 123-45-67", "+380671234567")]
    [InlineData("+380671234567", "+380671234567")]
    [InlineData("+38 067 123 45 67", "+380671234567")]
    [InlineData("380671234567", "+380671234567")]
    [InlineData("80671234567", "+380671234567")]
    [InlineData("671234567", "+380671234567")]
    [InlineData("  050.123.45.67  ", "+380501234567")]
    [InlineData("0414123456", "+380414123456")] // міський, Звягель
    [InlineData("0935554433", "+380935554433")]
    public void Normalizes_valid_numbers(string input, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("06712345")]          // коротко
    [InlineData("067123456789")]      // довго
    [InlineData("+48 512 345 678")]   // не український
    [InlineData("+7 912 345 67 89")]
    [InlineData("0071234567")]        // після +380 не може йти 0
    [InlineData("0171234567")]        // і 1
    [InlineData("067 123 45 6x")]
    [InlineData("067+1234567")]       // плюс не на початку
    [InlineData("067/123/45/67")]
    public void Rejects_invalid_numbers(string? input)
    {
        Assert.False(PhoneNumber.TryNormalize(input, out _));
    }

    [Fact]
    public void Formats_for_display()
    {
        Assert.Equal("+380 67 123 45 67", PhoneNumber.Format("+380671234567"));
    }
}
