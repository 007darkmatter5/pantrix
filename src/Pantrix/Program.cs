using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Pantrix.Components;
using Pantrix.Data;
using Pantrix.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<PantrixDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Pantrix")));

// Pages and services ask for the usual context factory and get one tied to the signed-in account's kitchen.
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<IDbContextFactory<PantrixDbContext>, KitchenDbContextFactory>();

builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<StoreAisleService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<KitchenService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<AddressLookupService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);

    // The OpenStreetMap services require requests to identify the application making them.
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Pantrix/1.0 (personal meal planning app)");
});

builder.Services.AddHttpClient<StoreBrowserService>(client => client.Timeout = TimeSpan.FromSeconds(15));

// Everything needs a signed-in user unless it says otherwise ([AllowAnonymous]: the sign-in pages, static files, health check).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Pantrix.Auth";
        options.LoginPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddCascadingAuthenticationState();

// The keys that encrypt sign-in cookies. In a container they must live on a mounted folder, or every restart
// would sign everyone out.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Pantrix");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.AddHealthChecks();

var app = builder.Build();

using (var db = new PantrixDbContext(app.Services.GetRequiredService<DbContextOptions<PantrixDbContext>>()))
{
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapHealthChecks("/healthz").AllowAnonymous();

// A plain link rather than a form: being signed out by a hostile page is a nuisance, not a breach.
app.MapGet("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return Results.Redirect("/account/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
