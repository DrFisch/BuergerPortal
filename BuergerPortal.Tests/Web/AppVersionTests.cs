using BuergerPortal.Web.Services;

namespace BuergerPortal.Tests.Web
{
    /// <summary>Versionsanzeige in der Fußzeile: Nummer aus Directory.Build.props, kurzer Commit aus dem Build.</summary>
    public class AppVersionTests
    {
        [Theory]
        [InlineData("1.1+abc1234", "1.1 (abc1234)")]                                  // Container: Hash aus deploy.ps1
        [InlineData("1.1+211bc1a6bc943f257ad4be755159a4b87c8e1927", "1.1 (211bc1a)")] // lokal: voller Hash aus Git
        [InlineData("1.1", "1.1")]                                                    // ohne Commit gebaut
        [InlineData(null, "?")]
        public void Version_wird_mit_kurzem_Commit_angezeigt(string? informationalVersion, string expected)
        {
            Assert.Equal(expected, AppVersion.Format(informationalVersion));
        }

        [Fact]
        public void Portal_traegt_die_Nummer_aus_Directory_Build_props()
        {
            // Gleiche Nummer wie die Testassembly (beide aus Directory.Build.props), nicht die SDK-Vorgabe 1.0.0
            var version = typeof(AppVersion).Assembly.GetName().Version!;
            Assert.Equal(typeof(AppVersionTests).Assembly.GetName().Version, version);
            Assert.NotEqual(new Version(1, 0, 0, 0), version);
            Assert.StartsWith($"{version.Major}.{version.Minor}", AppVersion.Display);
        }
    }
}
