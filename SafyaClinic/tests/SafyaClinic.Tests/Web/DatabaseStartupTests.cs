using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SafyaClinic.Web.Startup;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>S8 / F19: only Development, and only when enabled, migrates at start-up.</summary>
public class DatabaseStartupTests
{
    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "SafyaClinic.Web";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? migrateOnStartup) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseStartup.MigrateOnStartupKey] = migrateOnStartup })
            .Build();

    [Fact]
    public void Development_with_the_setting_migrates()
    {
        Assert.True(DatabaseStartup.ShouldMigrate(new Env { EnvironmentName = Environments.Development }, Config("true")));
    }

    [Fact]
    public void Development_without_the_setting_does_not_migrate()
    {
        Assert.False(DatabaseStartup.ShouldMigrate(new Env { EnvironmentName = Environments.Development }, Config(null)));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Other_environments_never_migrate_even_with_the_setting(string environment)
    {
        Assert.False(DatabaseStartup.ShouldMigrate(new Env { EnvironmentName = environment }, Config("true")));
    }
}