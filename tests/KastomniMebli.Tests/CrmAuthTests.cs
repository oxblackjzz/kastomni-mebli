using System.Net;
using System.Text.RegularExpressions;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KastomniMebli.Tests;

public partial class CrmAuthTests
{
    private static HttpClient Client(SiteFactory f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Вхід як у браузері: відкрити форму (антифорджері-токен), надіслати логін і пароль.</summary>
    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string login, string password)
    {
        var page = await client.GetStringAsync(AuthEndpoints.LoginPath);
        var token = AntiforgeryToken().Match(page).Groups[1].Value;
        return await client.PostAsync(AuthEndpoints.LoginSubmitPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["login"] = login,
            ["password"] = password,
            ["returnUrl"] = "/crm",
        }));
    }

    [Fact]
    public async Task Crm_requires_login()
    {
        await using var f = new SiteFactory();

        var response = await Client(f).GetAsync("/crm");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(AuthEndpoints.LoginPath, response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Correct_password_logs_in_wrong_does_not()
    {
        await using var f = new SiteFactory();
        await f.AddUserAsync("brat", "password123", Roles.Manager);
        var client = Client(f);

        var bad = await LoginAsync(client, "brat", "wrong-password");
        Assert.Contains("error=1", bad.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/crm")).StatusCode);

        var ok = await LoginAsync(client, "BRAT", "password123");   // логін без урахування регістру
        Assert.Equal("/crm", ok.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/crm")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/crm/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Installer_cannot_open_money_pages()
    {
        await using var f = new SiteFactory();
        await f.AddUserAsync("kum", "password123", Roles.Installer);
        var client = Client(f);
        await LoginAsync(client, "kum", "password123");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/crm")).StatusCode);
        foreach (var page in new[] { "/crm/dashboard", "/crm/klienty", "/crm/zamovlennia/nove" })
        {
            var r = await client.GetAsync(page);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.StartsWith(AuthEndpoints.DeniedPath, r.Headers.Location!.AbsolutePath);
        }
    }

    [Fact]
    public async Task Deactivated_user_is_logged_out()
    {
        await using var f = new SiteFactory();
        var user = await f.AddUserAsync("kum", "password123", Roles.Installer);
        var client = Client(f);
        await LoginAsync(client, "kum", "password123");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/crm")).StatusCode);

        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<UserService>().SetActiveAsync(new CurrentUser(0, "a", Roles.Admin), user.Id, false);

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/crm")).StatusCode);
    }

    [Fact]
    public async Task Files_are_served_only_to_allowed_users()
    {
        await using var f = new SiteFactory();
        await f.AddUserAsync("brat", "password123", Roles.Manager);
        var kum = await f.AddUserAsync("kum", "password123", Roles.Installer);

        int fileId;
        using (var scope = f.Services.CreateScope())
        {
            var admin = new CurrentUser(0, "a", Roles.Admin);
            var orders = scope.ServiceProvider.GetRequiredService<OrderService>();
            var files = scope.ServiceProvider.GetRequiredService<FileService>();
            var orderId = await orders.CreateAsync(admin, new NewOrderInput { ClientName = "А", Source = "brother" });
            using var content = new MemoryStream("%PDF-1.4 креслення"u8.ToArray());
            fileId = (await files.UploadAsync(admin, orderId, "drawing", "шафа.pdf", content, content.Length)).Id;
        }

        var anonymous = await Client(f).GetAsync($"/crm/fajly/{fileId}");
        Assert.NotEqual(HttpStatusCode.OK, anonymous.StatusCode);

        var brat = Client(f);
        await LoginAsync(brat, "brat", "password123");
        var ok = await brat.GetAsync($"/crm/fajly/{fileId}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("application/pdf", ok.Content.Headers.ContentType!.MediaType);
        Assert.Contains("креслення", await ok.Content.ReadAsStringAsync());

        // Монтажник не призначений на це замовлення — файл не бачить.
        var kumClient = Client(f);
        await LoginAsync(kumClient, "kum", "password123");
        Assert.Equal(HttpStatusCode.NotFound, (await kumClient.GetAsync($"/crm/fajly/{fileId}")).StatusCode);
    }

    [Theory]
    [InlineData("/crm/zamovlennia/5", "/crm/zamovlennia/5")]
    [InlineData("https://evil.example/crm", "/crm")]
    [InlineData("//evil.example/crm", "/crm")]
    [InlineData("/crm\\..\\x", "/crm")]
    [InlineData(null, "/crm")]
    public void Return_url_stays_inside_crm(string? input, string expected)
    {
        Assert.Equal(expected, AuthEndpoints.SafeReturnUrl(input));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
