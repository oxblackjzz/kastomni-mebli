using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using Microsoft.Extensions.Configuration;

namespace KastomniMebli.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task First_admin_comes_from_env_only_once()
    {
        var ctx = new CrmTestContext();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ADMIN_LOGIN"] = "Vova",
            ["ADMIN_PASSWORD"] = "secret-pass",
        }).Build();

        await ctx.Users.EnsureAdminAsync(config);
        await ctx.Users.EnsureAdminAsync(config);

        var admin = Assert.Single(await ctx.Users.ListAsync());
        Assert.Equal("vova", admin.Login);
        Assert.Equal(Roles.Admin, admin.Role);
        Assert.NotNull(await ctx.Users.CheckPasswordAsync("vova", "secret-pass"));
        Assert.NotEqual("secret-pass", admin.PasswordHash);
    }

    [Fact]
    public async Task No_env_no_admin()
    {
        var ctx = new CrmTestContext();

        await ctx.Users.EnsureAdminAsync(new ConfigurationBuilder().Build());

        Assert.Empty(await ctx.Users.ListAsync());
    }

    [Fact]
    public async Task Validation_and_permissions()
    {
        var ctx = new CrmTestContext();
        var manager = await ctx.UserAsync("brat", Roles.Manager, "Брат");

        await Assert.ThrowsAsync<CrmException>(() => ctx.Users.CreateAsync(CrmTestContext.Admin, new UserInput("x", "Хтось", Roles.Installer, null, null), "password123"));
        await Assert.ThrowsAsync<CrmException>(() => ctx.Users.CreateAsync(CrmTestContext.Admin, new UserInput("kum", "Кум", Roles.Installer, null, null), "short"));
        await Assert.ThrowsAsync<CrmException>(() => ctx.Users.CreateAsync(CrmTestContext.Admin, new UserInput("brat", "Дубль", Roles.Installer, null, null), "password123"));
        await Assert.ThrowsAsync<CrmForbiddenException>(() => ctx.Users.CreateAsync(manager, new UserInput("kum", "Кум", Roles.Installer, null, null), "password123"));
    }

    [Fact]
    public async Task Last_admin_cannot_be_disabled_or_demoted()
    {
        var ctx = new CrmTestContext();
        var admin = await ctx.UserAsync("vova", Roles.Admin, "Вова");

        await Assert.ThrowsAsync<CrmException>(() => ctx.Users.SetActiveAsync(CrmTestContext.Admin, admin.Id, false));
        await Assert.ThrowsAsync<CrmException>(() => ctx.Users.UpdateAsync(CrmTestContext.Admin, admin.Id, new UserInput("vova", "Вова", Roles.Manager, null, null)));
    }

    [Fact]
    public async Task Disabled_user_cannot_log_in()
    {
        var ctx = new CrmTestContext();
        await ctx.UserAsync("vova", Roles.Admin, "Вова");
        var kum = await ctx.UserAsync("kum", Roles.Installer, "Кум");

        await ctx.Users.SetActiveAsync(CrmTestContext.Admin, kum.Id, false);

        Assert.Null(await ctx.Users.CheckPasswordAsync("kum", "password123"));
    }
}
