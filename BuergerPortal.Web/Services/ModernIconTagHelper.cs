using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Symbol aus dem Satz von Portal 2: <c>&lt;m-icon name="calendar" /&gt;</c> wird zu
/// <c>&lt;svg class="m-i"&gt;&lt;use href="/modern/symbole.svg?v=…#calendar"/&gt;&lt;/svg&gt;</c>. Die Datei trägt wie
/// Skripte und Stile eine Versionskennung (neue Symbole kommen sofort an). Ohne <c>label</c> ist das Symbol Schmuck
/// (<c>aria-hidden</c>); mit <c>label</c> bekommt es eine Beschriftung für Screenreader.
/// </summary>
[HtmlTargetElement("m-icon", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ModernIconTagHelper(IFileVersionProvider fileVersionProvider) : TagHelper
{
    public const string SpritePath = "/modern/symbole.svg";

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public string Name { get; set; } = "";

    public string? Label { get; set; }

    public string? Class { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var sprite = fileVersionProvider.AddFileVersionToPath(ViewContext.HttpContext.Request.PathBase, SpritePath);
        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", string.IsNullOrWhiteSpace(Class) ? "m-i" : "m-i " + Class);
        output.Attributes.SetAttribute("focusable", "false");
        if (string.IsNullOrWhiteSpace(Label))
        {
            output.Attributes.SetAttribute("aria-hidden", "true");
        }
        else
        {
            output.Attributes.SetAttribute("role", "img");
            output.Attributes.SetAttribute("aria-label", Label);
        }
        output.Content.SetHtmlContent($"<use href=\"{System.Net.WebUtility.HtmlEncode(sprite)}#{System.Net.WebUtility.HtmlEncode(Name)}\"></use>");
    }
}
