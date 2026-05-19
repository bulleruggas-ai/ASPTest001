var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddHttpClient("Supabase", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url is not configured.");
    var key = config["Supabase:Key"] ?? throw new InvalidOperationException("Supabase:Key is not configured.");

    client.BaseAddress = new Uri(url.TrimEnd('/') + "/rest/v1/");
    client.DefaultRequestHeaders.Add("apikey", key);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
