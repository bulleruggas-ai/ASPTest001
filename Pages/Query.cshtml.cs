using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StartupSite.Pages;

public class QueryModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<QueryModel> _logger;

    public QueryModel(IHttpClientFactory httpClientFactory, ILogger<QueryModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty]
    public QueryInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var payload = new
        {
            name = Input.Name,
            email = Input.Email,
            subject = Input.Subject,
            message = Input.Message
        };

        var client = _httpClientFactory.CreateClient("Supabase");
        var request = new HttpRequestMessage(HttpMethod.Post, "queries")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Prefer", "return=minimal");

        try
        {
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Supabase insert failed ({Status}): {Body}", response.StatusCode, body);
                ErrorMessage = "Sorry, we couldn't submit your query. Please try again.";
                return Page();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Supabase request failed");
            ErrorMessage = "Sorry, we couldn't reach the server. Please try again.";
            return Page();
        }

        SuccessMessage = "Thanks! Your query has been submitted.";
        return RedirectToPage();
    }

    public class QueryInput
    {
        [Required]
        [Display(Name = "Name")]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Subject")]
        [StringLength(200)]
        public string? Subject { get; set; }

        [Required]
        [Display(Name = "Message")]
        [StringLength(4000, MinimumLength = 5)]
        public string Message { get; set; } = string.Empty;
    }
}
