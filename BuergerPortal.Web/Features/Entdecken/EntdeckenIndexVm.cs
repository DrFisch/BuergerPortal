namespace BuergerPortal.Web.Features.Entdecken
{
    public class EntdeckenIndexVm
    {
        public List<PoiListItemVm> Orte { get; set; } = new();
    }

    public class PoiListItemVm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Cat { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string? Addr { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
    }
}
