// Copyright ©2026 Scott Blomfield

using System.Globalization;
using JumpStart.Api.Clients;
using JumpStart.Services;
using JumpStart.Services.Authentication;
using JumpStart.Services.Authentication.Clients;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RustArchon.Panel.Clients;
using RustArchon.Panel.Components;
using RustArchon.Panel.Components.Account;
using RustArchon.Panel.Data;
using RustArchon.Panel.Infrastructure;
using RustArchon.Panel.Localization;
using RustArchon.Panel.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. BLAZOR COMPONENTS
// ============================================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ============================================
// 1B. LOCALIZATION
// ============================================
// Standard Microsoft.Extensions.Localization pipeline (AddLocalization, IStringLocalizer<T>,
// RequestLocalizationOptions) - the only non-standard piece is where the strings themselves live.
// See RustArchon.Panel/Resources/README.md for the whole story: one shared JSON file per language
// (SharedResource.<culture>.json, physical files copied next to the built app - see the .csproj),
// translated collaboratively through Hosted Weblate rather than a hand-rolled admin UI or one .resx
// per component. AddSingleton<IStringLocalizerFactory> must come before AddLocalization() (which only
// registers the factory if one isn't already present) - see JsonFileStringLocalizerFactory's own
// remarks for why this factory exists instead of a package.
builder.Services.AddSingleton<IStringLocalizerFactory, JsonFileStringLocalizerFactory>();
builder.Services.AddLocalization();

// The one list every supported culture is named in - both JsonFileStringLocalizer's culture-fallback
// (which JSON files it looks for) and RequestLocalizationOptions below (what negotiation/the switcher
// may offer) ultimately trace back to this same array, so there is exactly one place to add a language
// rather than two that could drift. English only for now (CultureSelector.razor hides itself entirely
// while this array has just one entry) - the mechanism itself was verified end-to-end against a
// temporary es-ES file before this was written, see Resources/README.md for how to actually add one.
var supportedCultures = new[] { new CultureInfo("en-US") };

// Registered via Configure (not just a local RequestLocalizationOptions instance passed straight to
// UseRequestLocalization below) so CultureSelector.razor can also read it as IOptions<RequestLocalizationOptions>
// to populate the language list - one source of truth for "what languages does the switcher offer",
// not a second copy of supportedCultures living in the component.
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(supportedCultures[0]);
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

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

    // A site admin opening a customer's server arrives at ?tenantId=<theirs>, which they are not a
    // member of. Without this the client would quietly redirect them to their own Organization
    // before the Api was ever asked. It grants nothing by itself - the Api re-validates every
    // exchange against SiteAdminCrossTenantPolicy, which refuses anybody else.
    options.AllowCrossTenantSelection = true;
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

// Same handler chain again - gated by Platform.ManagePlans (see PlansController), independent of the
// other two Site Admin permissions above.
builder.Services.AddApiClient<IPlanApiClient>($"{apiBaseUrl}/api/plans")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Same handler chain, but no admin permission behind it - this one is any signed-in member acting on
// their OWN Organization's subscription (the API resolves the tenant from the token, never from the
// request), unlike IPlanApiClient above which manages the platform-wide catalog.
builder.Services.AddApiClient<ISubscriptionApiClient>($"{apiBaseUrl}/api/subscription")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Same handler chain again - gated by Platform.ManageOrganizations, the same permission behind the
// Organizations console and the user directory this is used from (see NotesController's remarks).
builder.Services.AddApiClient<INoteApiClient>($"{apiBaseUrl}/api/notes")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Same handler chain and permission again - the permanent communications log alongside Notes on the
// same two admin screens.
builder.Services.AddApiClient<ICommunicationApiClient>($"{apiBaseUrl}/api/communications")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Same handler chain and permission as IPlatformSettingsApiClient - editing what an email says is the
// same kind of platform-wide decision as everything else Platform.ManageSettings already covers.
builder.Services.AddApiClient<IEmailTemplateApiClient>($"{apiBaseUrl}/api/email-templates")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Same handler chain and permission again - the reusable placeholders email templates draw from.
builder.Services.AddApiClient<IEmailPlaceholderApiClient>($"{apiBaseUrl}/api/email-placeholders")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Same handler chain again - gated by Platform.ViewReports (see ReportsController), which is its own
// permission rather than a reuse of ManagePlans: reading what the business is owed and changing what it
// charges are different jobs.
builder.Services.AddApiClient<IReportApiClient>($"{apiBaseUrl}/api/reports")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Same handler chain again - gated by Platform.ManageBilling (see BillingController), its own
// permission rather than ViewReports: chasing an invoice and writing one off are different jobs, and
// only the second moves money in the books.
builder.Services.AddApiClient<IBillingApiClient>($"{apiBaseUrl}/api/billing")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Same handler chain again - gated by Platform.ManageOrganizations (see OrganizationsController). The
// only client here that reads and acts on an Organization other than the signed-in user's own, which is
// why it has a permission to itself rather than sharing ViewReports.
builder.Services.AddApiClient<IOrganizationApiClient>($"{apiBaseUrl}/api/admin/organizations")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// The platform-wide user directory and the Site Admin role, same permission as the console above.
builder.Services.AddApiClient<IPlatformUserApiClient>($"{apiBaseUrl}/api/admin/users")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// The signed-in member's OWN Organization - its roles and its people. Same handler chain as the
// subscription client above and for the same reason: the Api resolves the tenant from the token, so
// neither of these can be pointed at somebody else's Organization the way IOrganizationApiClient can.
builder.Services.AddApiClient<IOrganizationRoleApiClient>($"{apiBaseUrl}/api/organization/roles")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

builder.Services.AddApiClient<IOrganizationMemberApiClient>($"{apiBaseUrl}/api/organization/members")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

builder.Services.AddApiClient<IOrganizationInvitationApiClient>($"{apiBaseUrl}/api/organization/invitations")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

builder.Services.AddApiClient<IOrganizationSettingsApiClient>($"{apiBaseUrl}/api/organization/settings")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Creating an Organization of one's own. Same handler chain, but note this one is not scoped to a
// tenant at all - the Organization does not exist yet, so the Api authorizes it on the caller alone.
builder.Services.AddApiClient<IOrganizationCreationApiClient>($"{apiBaseUrl}/api/organization")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// Accepting an invitation, which is two calls with different requirements against one controller.
// Reading it is anonymous - the person following the link may not have an account yet - so it gets
// no handlers at all; attaching the exchange handler would try to mint a token for nobody.
builder.Services.AddApiClient<IInvitationPreviewApiClient>($"{apiBaseUrl}/api/invitations/organization");

builder.Services.AddApiClient<IInvitationAcceptApiClient>($"{apiBaseUrl}/api/invitations/organization")
    .AddHttpMessageHandler<JwtExchangeHandler>()
    .AddHttpMessageHandler<JwtAuthenticationHandler>();

// The platform's own name and public site URL - anonymous, same no-handlers reasoning as
// IInvitationPreviewApiClient above: the nav bar (and the login page it renders on) needs this before
// anyone is signed in. SiteBrandingService reads this straight out of the same Valkey cache
// RustArchon.Api's own PlatformSettingsCache writes through to (see IValkeyCache below); this client
// is only its fallback for a cold cache.
builder.Services.AddApiClient<ISiteBrandingApiClient>($"{apiBaseUrl}/api/public/branding");
builder.Services.AddSingleton<SiteBrandingService>();

// Same Valkey container RustArchon.Api's own PlatformSettingsCache writes through to on every admin
// save - reading it directly here, rather than keeping a second Panel-local cache with its own
// invalidation to keep in sync, is what makes a saved change visible immediately everywhere: there is
// nothing of the Panel's own to go stale. Registered only when a connection string is actually
// configured, and resolved lazily via IServiceProvider inside ValkeyCache, for the identical reason
// RustArchon.Api's own PlatformSettingsCache does both - see its Program.cs remarks.
var valkeyConnectionString = builder.Configuration["Valkey:ConnectionString"];
if (!string.IsNullOrWhiteSpace(valkeyConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var options = ConfigurationOptions.Parse(valkeyConnectionString);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    });
}

builder.Services.AddSingleton<IValkeyCache, ValkeyCache>();

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

// Same channel, same key: clearing up the empty organization a registration leaves behind when it
// gets that far and then loses the race for its invitation code. Nobody to authenticate as at that
// point - the founding account is being deleted alongside it.
builder.Services.AddApiClient<IInternalRegistrationApiClient>(apiBaseUrl)
    .ConfigureHttpClient(client => client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey));

// Same channel again - backs the /track/email/{id}.gif endpoint below, the only caller.
builder.Services.AddApiClient<IInternalCommunicationApiClient>(apiBaseUrl)
    .ConfigureHttpClient(client => client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey));

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, QueuedEmailSender>();

// Live console/chat/status tail for a server's detail page - see RconHubClient's own remarks.
builder.Services.AddScoped<RconHubClient>();

builder.Services.AddHttpContextAccessor();

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

    app.MapGet("/single-user-auth/login", async (
        IConfiguration config,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        string? returnUrl) =>
    {
        var section = config.GetSection("SingleUserAuth");
        var username = section["Username"];

        if (!section.GetValue<bool>("Enabled") || string.IsNullOrWhiteSpace(username))
            return Results.LocalRedirect("/");

        var user = await userManager.FindByNameAsync(username)
                    ?? await userManager.FindByEmailAsync(username);

        // If user doesn't exist and we're trying to use SingleUserAuth, check for the admin registration
        if (user is null)
        {
            return Results.BadRequest($"SingleUserAuth: no user found for '{username}'.");
        }
        else
        { 
            await signInManager.SignInAsync(user, isPersistent: true);
            return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
        }
    });
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Recovers the original scheme/host from X-Forwarded-Proto/-For - see RustArchon.Api/Program.cs's
// matching remarks for why this is safe with KnownProxies/KnownNetworks left empty in this topology.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// The official .NET container base images set this to "true" - skip redirecting to HTTPS when the
// app only has an HTTP endpoint to begin with (ASPNETCORE_URLS=http://+:8080 in Dockerfile), which is
// the case in the Docker Compose setup. A reverse proxy in front of this container is where TLS
// termination belongs in that topology - see the README.
if (!builder.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
{
    app.UseHttpsRedirection();
}

// MapStaticAssets(), not UseStaticFiles() - the latter serves plain wwwroot files fine, but the
// fingerprinted/compressed assets the @Assets[...] tag helper resolves (App.razor's script tags,
// including the Blazor runtime itself at _framework/blazor.web.js) only exist in the endpoint map
// MapStaticAssets() builds from the published output's staticwebassets.endpoints.json manifest.
// Confirmed by hand: dotnet run's dev-time asset pipeline masks this - UseStaticFiles() alone works
// fine there - but a real `dotnet publish` build (this container's own build, via the Dockerfile) 404s
// on every @Assets[...] reference, including blazor.web.js itself, breaking all interactivity.
app.MapStaticAssets();

// A 1x1 transparent GIF, embedded in every Communication's HtmlBody by RustArchon.Api's
// CommunicationPublisher. Unauthenticated and unconditional (not gated to Development, unlike
// single-user-auth below) - a real recipient's mail client has to be able to load this in production.
// Lives here, not on RustArchon.Api directly, because the Api is never reachable from outside the
// Docker network (see its InternalController's remarks) - this Panel is the one public door, so it
// serves the pixel itself and makes one internal call to record the view.
var trackingPixel = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==");
app.MapGet("/track/email/{id:guid}.gif", async (Guid id, IInternalCommunicationApiClient client) =>
{
    try
    {
        await client.MarkViewedAsync(id);
    }
    catch
    {
        // Never let a broken or already-resolved tracking call turn into a broken image in
        // somebody's inbox - the pixel itself always loads regardless of what MarkViewedAsync did.
    }

    return Results.File(trackingPixel, "image/gif");
});

// Authentication & Authorization must run before UseAntiforgery, so HttpContext.User is already
// populated when antiforgery validates the token's embedded claims - otherwise every check compares
// against the wrong (unauthenticated) principal, causing AntiforgeryValidationException on every
// request, not just with stale cookies.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Placed immediately before MapRazorComponents per Microsoft's own guidance for Blazor Server/global
// Interactive Server apps - see App.razor's own remarks for how the resolved culture then survives
// for the life of the circuit despite Blazor Server never re-running this middleware per component.
// Uses the same RequestLocalizationOptions instance Configure<>() built above (see its own remarks),
// not a second one constructed here.
app.UseRequestLocalization();

// The redirect-based culture switcher - see Components/Shared/CultureSelector.razor, the only caller.
// A LocalRedirect, never anything else, to rule out open-redirect abuse of redirectUri.
app.MapGet("/Culture/Set", (string? culture, string redirectUri, HttpContext context) =>
{
    if (!string.IsNullOrWhiteSpace(culture))
    {
        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture, culture)));
    }

    return Results.LocalRedirect(redirectUri);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

app.Run();
