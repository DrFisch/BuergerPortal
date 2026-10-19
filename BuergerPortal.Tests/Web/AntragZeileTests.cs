using BuergerPortal.Web.Features.Antraege;

namespace BuergerPortal.Tests.Web
{
    /// <summary>„Meine Anträge“: Filter, Stufen des Fortschritts, Vorgangsnummer und Zeitleiste der Detailseite.</summary>
    public class AntragZeileTests
    {
        private static AntragZeileVm Antrag(int status, int typ = Leistungen.Sperrmuell) => new()
        {
            Id = Guid.Parse("9b4ede50-1111-2222-3333-444455556666"),
            Typ = typ,
            Status = status,
            Angelegt = new DateTime(2026, 10, 18, 9, 0, 0),
        };

        [Fact]
        public void Filter_teilen_nach_Status_auf()
        {
            var vm = new AntraegeStatusVm { Antraege = [Antrag(1), Antrag(2), Antrag(3), Antrag(4), Antrag(5)] };

            Assert.Equal(5, vm.Nach("alle").Count());
            Assert.Equal(2, vm.Nach("laufend").Count());
            Assert.Single(vm.Nach("entwuerfe"));
            Assert.Equal(2, vm.Nach("erledigt").Count());
            Assert.Equal("alle", AntraegeStatusVm.Normalisieren("<script>"));
            Assert.Equal("alle", AntraegeStatusVm.Normalisieren(null));
            Assert.Equal("erledigt", AntraegeStatusVm.Normalisieren("erledigt"));
        }

        [Fact]
        public void Abholtage_sind_die_naechsten_Werktage_ab_morgen()
        {
            // Freitag, 23.10.2026 → Montag bis Mittwoch der Folgewoche
            var tage = BuergerPortal.Web.Features.Antraege.Sperrmuell.ViewModels.SperrmuellStep2Vm
                .NaechsteWerktage(new DateOnly(2026, 10, 23), 3);
            Assert.Equal([new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 28)], tage);
        }

        [Fact]
        public void Stufen_und_Vorgangsnummer()
        {
            Assert.Equal(0, Antrag(1).Stufe);
            Assert.Equal(1, Antrag(2).Stufe);
            Assert.Equal(2, Antrag(3).Stufe);
            Assert.Equal(3, Antrag(4).Stufe);
            Assert.Equal("9B4EDE50", Antrag(2).Vorgangsnummer);
            Assert.Equal("SperrmuellStep2", Antrag(1).Leistung!.EntwurfAction);
            Assert.Equal("Antrag", Antrag(2, Leistungen.Reisepass).Leistung!.DetailAction);
        }

        [Theory]
        [InlineData(1, false, false, false, false)]
        [InlineData(2, true, false, false, true)]
        [InlineData(3, true, true, false, true)]
        [InlineData(4, true, true, true, false)]
        [InlineData(5, true, true, true, false)]
        public void Zeitleiste_folgt_dem_Status(int status, bool eingereicht, bool inBearbeitung, bool abgeschlossen,
            bool hinweis)
        {
            var vm = new AntragVerlaufVm(status, new DateTime(2026, 10, 18, 9, 0, 0), null, null, null);

            Assert.Equal(eingereicht, vm.IstEingereicht);
            Assert.Equal(inBearbeitung, vm.IstInBearbeitung);
            Assert.Equal(abgeschlossen, vm.IstAbgeschlossen);
            Assert.Equal(hinweis, vm.ZeigeSimulationsHinweis);
        }
    }
}
