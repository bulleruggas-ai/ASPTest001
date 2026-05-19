using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StartupSite.Pages;

public class IndexModel : PageModel
{
    public string CompanyName { get; } = "Exxaro Stack Test";
    public string Tagline { get; } = "Smarter, greener cities — one rooftop at a time.";
    public string Mission { get; } =
        "We help building owners turn unused rooftops into productive, sensor-monitored " +
        "micro-farms that cut cooling costs, capture stormwater, and supply fresh produce " +
        "to the communities directly below them.";
    public int FoundedYear { get; } = 2026;
    public string Headquarters { get; } = "Pretoria, South Africa";
    public int TeamSize { get; } = 7;
    public string ContactEmail { get; } = "hello@example.com";

    public (string Title, string Body, string Icon)[] Offerings { get; } = new[]
    {
        ("Modular Rooftop Kits",
         "Pre-fabricated growing beds with built-in irrigation that install in a single afternoon.",
         "🌱"),
        ("Live Sensor Dashboard",
         "Soil, water, and microclimate data streamed to a single dashboard so nothing is guesswork.",
         "📈"),
        ("Harvest-to-Tenant Program",
         "We coordinate weekly produce deliveries to tenants and local food banks on your behalf.",
         "🥬")
    };

    public void OnGet() { }
}
