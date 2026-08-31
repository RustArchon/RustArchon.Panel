// Copyright ©2026 Scott Blomfield

using JumpStart.Api.Clients;
using JumpStart.Services;
using JumpStart.Services.Authentication;
using JumpStart.Services.Authentication.Clients;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RustArchon.Panel.Clients;
using RustArchon.Panel.Components;
using RustArchon.Panel.Components.Account;
using RustArchon.Panel.Data;
using RustArchon.Panel.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. BLAZOR COMPONENTS
// ============================================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ============================================
// 2. DATABASE CONTEXT (Identity only)
// ============================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ============================================
// 3. IDENTITY SERVICES
// ============================================

// Persisted, named Data Protection key ring - without this, both this cookie and every antiforgery
// token silently invalidate on every restart (container recreation, redeploy), since ASP.NET Core
// otherwise falls back to an ephemeral per-machine key. SessionKeyPath defaults to a folder shared
// with RustArchon.Web (one level up from each project - the umbrella repo root), NOT
// RustArchon.Api's own /keys volume (a separate key ring for a separate purpose - RCON password
// encryption, see RustArchon.Api/Program.cs). RustArchon.Web must use the exact same ApplicationName
// and point at the exact same physical location, or it can never decrypt this cookie - see its
// Program.cs and the README's cross-app session section.
var sessionKeyPath = builder.Configuration["DataProtection:SessionKeyPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "App_Data", "dataprotection-keys-session");
Directory.CreateDirectory(sessionKeyPath);

builder.Services.AddDataProtection()
    .SetApplicationName("RustArchon.Session")
    .PersistKeysToFileSystem(new DirectoryInfo(sessionKeyPath));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// CookieDomain left unset locally - localhost:5100/:5200 already share cookies as the same hostname
// on different ports, no Domain attribute needed. In production, www.rustarchon.com and
// panel.rustarchon.com are genuinely different hostnames - set CookieDomain=.rustarchon.com (the
// leading dot covers the parent domain and every subdomain) so RustArchon.Web actually receives this
// cookie at all. This is what makes cross-subdomain SSO possible; the shared Data Protection key
// ring above is what makes it *verifiable* once received.
var cookieDomain = builder.Configuration["CookieDomain"];
if (!string.IsNullOrEmpty(cookieDomain))
{
    builder.Services.ConfigureApplicationCookie(options => options.Cookie.Domain = cookieDomain);
}

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// ============================================
// 4. JWT TOKEN SERVICES (for API calls)
// ============================================
// AddJwtTokenService() binds JwtTokenOptions from the "JwtSettings" section by default (see
// JumpStart's ADR-016) - PostConfigure overrides just SecretKey to come from the flat
// RUSTARCHON_JWT_SECRET_KEY key, the same source RustArchon.Api's own JWT bearer validation setup
// uses, so both processes trust the same key.
builder.Services.AddJwtTokenService();
builder.Services.PostConfigure<JwtTokenOptions>(options =>
    options.SecretKey = builder.Configuration["RUSTARCHON_JWT_SECRET_KEY"] ?? options.SecretKey);
builder.Services.AddScoped<ITokenStore, TokenStore>();
builder.Services.AddTransient<JwtAuthenticationHandler>();
builder.Services.AddTransient<JwtExchangeHandler>();
builder.Services.AddTransient<TokenBridgeHandler>();

// API-client-based tenant selection - every user has exactly one tenant today (provisioned at
// sign-up by NewTenantBootstrapper), but MainLayout's <TenantSwitcher> and future teammate-invite
// support both need this registered regardless. JwtExchangeHandler picks it up automatically
// (resolved lazily, not via constructor injection) to add a tenant_id claim to the identity
// assertion. ITenantsApiClient itself is auto-discovered below (AutoDiscoverApiClients).
builder.Services.AddScoped<ITenantSelectionService, ApiTenantSelectionService>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7130";

// Token-exchange client passes its bearer token explicitly per call (see JwtExchangeHandler) - it
// must not go through JwtAuthenticationHandler, which would attach whatever's currently in
// ITokenStore (nothing, the first time). Registered before AddJumpStart so RegisterApiClients' JWT
// auto-attachment check sees ITokenExchangeApiClient as already present.
builder.Services.AddApiClient<ITokenExchangeApiClient>(apiBaseUrl);

// ============================================
// 5. JUMPSTART / API CLIENT REGISTRATION
// ============================================
builder.Services.AddJumpStart(options =>
{
    options.ApiBaseUrl = apiBaseUrl;
    options.AutoDiscoverApiClients = true;   // discovers ITenantsApiClient, IRolesApiClient, ...
    options.AutoDiscoverRepositories = false; // no local repositories - this project has none
});

// Account-bootstrap client + service - provisions a tenant and Owner role for a first-time user.
// Called directly from Register.razor/ExternalLogin.razor right after account creation, not via a
// message handler - see NewTenantBootstrapper's remarks.
builder.Services.AddApiClient<IAccountBootstrapApiClient>(apiBaseUrl);
builder.Services.AddScoped<NewTenantBootstrapper>();

// IRustServerApiClient can't use [ApiClientFor<...>] auto-attachment (see its remarks), so its
// handler chain is wired by hand. Handler order (first added = outermost, runs first):
// JwtExchangeHandler ensures a real token exists; JwtAuthenticationHandler attaches whatever's now
// in ITokenStore.
builder.Services.AddApiClient<IRustServerApiClient>($"{apiBaseUrl}/api/rustservers")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>()
    // Innermost (runs last, right before the request is actually sent, after a real token is
    // guaranteed to exist) - bridges it into the real circuit's own ITokenStore, since
    // RconHubClient reads that one directly rather than through an API client's HTTP pipeline. See
    // TokenBridgeHandler's remarks for why that bridge is necessary at all.
    .AddHttpMessageHandler<TokenBridgeHandler>();

// Same handler chain as IRustServerApiClient - only the account matching the API's
// RUSTARCHON_ADMIN_EMAIL can actually use this; everyone else's calls 403.
builder.Services.AddApiClient<IInvitationCodeApiClient>($"{apiBaseUrl}/api/invitation-codes")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Same handler chain again - gated by the "Site Admin" role's Platform.ManageSettings permission
// instead (see PlatformSettingsController), granted independently of Platform.ManageInvitations above
// even though both currently land on the same account by default.
builder.Services.AddApiClient<IPlatformSettingsApiClient>($"{apiBaseUrl}/api/platform-settings")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Deliberately no JWT handlers - Register.razor calls this before an account exists, so there's no
// token to attach yet. See IInvitationApiClient's remarks.
builder.Services.AddApiClient<IInvitationApiClient>($"{apiBaseUrl}/api/invitations");

// Deliberately no JWT handlers, same reasoning as IInvitationApiClient - but this one authenticates
// with the shared internal-service secret instead of nothing, since it must always be usable
// regardless of who (if anyone) is signed in. See QueuedEmailSender's remarks. Read directly from the
// flat RUSTARCHON_INTERNAL_API_KEY key - the exact same name RustArchon.Api and RustArchon.Worker
// also read, with no rename in between.
var internalApiKey = builder.Configuration["RUSTARCHON_INTERNAL_API_KEY"]
    ?? throw new InvalidOperationException("RUSTARCHON_INTERNAL_API_KEY configuration is missing.");
builder.Services.AddApiClient<IInternalEmailApiClient>(apiBaseUrl)
    .ConfigureHttpClient(client => client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey));
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, QueuedEmailSender>();

// Live console/chat/status tail for a server's detail page - see RconHubClient's own remarks.
builder.Services.AddScoped<RconHubClient>();

var app = builder.Build();

// ============================================
// APPLY PENDING MIGRATIONS (Identity only)
// ============================================
// Convenience for local development so the app "just runs" against a fresh LocalDB instance with no
// manual `dotnet ef database update` step. Not appropriate for production services with multiple
// scaled-out instances (concurrent migration application).
using (var migrationScope = app.Services.CreateScope())
{
    migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

// ============================================
// MIDDLEWARE PIPELINE
// ============================================
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// The official .NET container base images set this to "true" - skip redirecting to HTTPS when the
// app only has an HTTP endpoint to begin with (ASPNETCORE_URLS=http://+:8080 in Dockerfile), which is
// the case in the Docker Compose setup. A reverse proxy in front of this container is where TLS
// termination belongs in that topology - see the README.
if (!builder.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

// Authentication & Authorization must run before UseAntiforgery, so HttpContext.User is already
// populated when antiforgery validates the token's embedded claims - otherwise every check compares
// against the wrong (unauthenticated) principal, causing AntiforgeryValidationException on every
// request, not just with stale cookies.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

app.Run();
