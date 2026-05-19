using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StartupSite.Pages;

public class ObjectsModel : PageModel
{
    private const string ApiUrl = "https://api.restful-api.dev/objects";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ObjectsModel> _logger;

    public ObjectsModel(IHttpClientFactory httpClientFactory, ILogger<ObjectsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<ApiObject> Objects { get; private set; } = new();
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        try
        {
            var result = await client.GetFromJsonAsync<List<ApiObject>>(
                ApiUrl,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
            Objects = result ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Url}", ApiUrl);
            ErrorMessage = "Could not reach the API. Please try again.";
        }
    }

    public class ApiObject
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, JsonElement>? Data { get; set; }
    }
}
