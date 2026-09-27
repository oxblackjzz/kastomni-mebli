using KastomniMebli.Web.Data;
using Npgsql;

namespace KastomniMebli.Tests;

public class DatabaseUrlTests
{
    [Fact]
    public void Converts_render_url()
    {
        var cs = DatabaseUrl.ToNpgsql("postgres://kastomni:p%40ss%3Aword@dpg-abc123-a.frankfurt-postgres.render.com:5433/kastomni_mebli");
        var b = new NpgsqlConnectionStringBuilder(cs);

        Assert.Equal("dpg-abc123-a.frankfurt-postgres.render.com", b.Host);
        Assert.Equal(5433, b.Port);
        Assert.Equal("kastomni", b.Username);
        Assert.Equal("p@ss:word", b.Password);
        Assert.Equal("kastomni_mebli", b.Database);
        Assert.Equal(SslMode.Prefer, b.SslMode);
    }

    [Fact]
    public void Uses_default_port_and_sslmode_from_query()
    {
        var b = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToNpgsql("postgresql://u:p@host/db?sslmode=require"));

        Assert.Equal(5432, b.Port);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Fact]
    public void Leaves_npgsql_format_untouched()
    {
        const string cs = "Host=localhost;Database=kastomni_mebli;Username=postgres";

        Assert.Equal(cs, DatabaseUrl.ToNpgsql(cs));
    }
}
