using BuergerPortal.BundId;

namespace BuergerPortal.Tests.BundId
{
    public class TrustLevelTests
    {
        [Theory]
        [InlineData("STORK-QAA-Level-1", 1)]
        [InlineData("STORK-QAA-Level-3", 3)]
        [InlineData(" STORK-QAA-Level-4 ", 4)]
        [InlineData("http://eidas.europa.eu/LoA/low", 1)]
        [InlineData("http://eidas.europa.eu/LoA/substantial", 3)]
        [InlineData("http://eidas.europa.eu/LoA/high", 4)]
        public void Parse_erkennt_STORK_und_eIDAS(string value, int expected)
        {
            Assert.Equal(expected, TrustLevel.Parse(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("urn:oasis:names:tc:SAML:2.0:ac:classes:Password")]
        [InlineData("STORK-QAA-Level-9")]
        public void Parse_liefert_null_bei_unbekannten_Werten(string? value)
        {
            Assert.Null(TrustLevel.Parse(value));
        }

        [Theory]
        [InlineData(1, "normal")]
        [InlineData(3, "substanziell")]
        [InlineData(4, "hoch")]
        public void Describe_liefert_verstaendliche_Bezeichnung(int level, string expected)
        {
            Assert.Equal(expected, TrustLevel.Describe(level));
        }

        [Fact]
        public void ToStork_erzeugt_den_Bezeichner_fuer_den_AuthnRequest()
        {
            Assert.Equal("STORK-QAA-Level-3", TrustLevel.ToStork(TrustLevel.Substantial));
        }
    }
}
