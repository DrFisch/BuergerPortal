using BuergerPortal.Web.Features.Antraege;

namespace BuergerPortal.Tests.Web
{
    /// <summary>„Meine Anträge“: Filter, Stufen des Fortschritts und Vorgangsnummer.</summary>
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
    }
}
