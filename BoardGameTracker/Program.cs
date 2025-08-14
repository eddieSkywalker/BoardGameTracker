using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor.Services;
using BoardGameTracker.Services;
using BoardGameTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);
var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);
logger.LogInformation("Content Root: {ContentRoot}", builder.Environment.ContentRootPath);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMudServices();

if (builder.Environment.IsDevelopment())
{
    // Configures data protection to persist encryption keys to a local file system directory.
    // This is used for shared cookie authentication in local development environments.
    // The application name ensures keys are isolated for this specific app.
    // This setup is primarily for local testing and should be wrapped in an IsDevelopment check to avoid being used in production.
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["LocalOnlyConfig:SharedKeyPath"]))
        .SetApplicationName("BoardGameTrackerSSO");

    // Configures Kestrel to listen on localhost (127.0.0.1) at port 5003.
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(System.Net.IPAddress.Parse("127.0.0.1"), 5003, listenOptions =>
        {
            listenOptions.UseHttps(
                builder.Configuration["LocalOnlyConfig:Certificates:Path"],
                builder.Configuration["LocalOnlyConfig:Certificates:Password"]);
        });
    });
}

// Add session support for token storage
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
})
.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    // LocalHost development needs custom domains set in local "Hosts" file 
    // in order to have the same shared-domain cookie functionality for auto authentication.
    // For example, add the following line to your Hosts file: 127.0.0.1 App1.localtest.me
    // If not, you will be requested to login each time for first time you access an app.
    options.Cookie.Name = "BoardGameTrackerCookie";
    options.Cookie.Domain = builder.Configuration["Cookie:Domain"];
    options.Cookie.Path = "/";
    // Use Lax for cross-site cookies
    options.Cookie.SameSite = SameSiteMode.Lax; 
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // options.Cookie.SameSite = SameSiteMode.None; // Use None for cross-site cookies in production
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    // 1. Your app trusts Cognito as the Identity Provider
    options.Authority = builder.Configuration["Cognito:Authority"];
    // 2. These are your app's credentials with Cognito:
    options.ClientId = builder.Configuration["Cognito:ClientId"];
    options.ClientSecret = builder.Configuration["Cognito:ClientSecret"];
    // 3. Protocol specifications: OpenID Connect, OAuth 2.0 "Authorization Code" flow
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.CallbackPath = "/signin-oidc";
    options.SignedOutCallbackPath = "/authorization/loggedout";
    options.RequireHttpsMetadata = true;
    // Save the access and ID tokens in the authentication session/cookie.
    options.SaveTokens = true; //TODO Not sure if still needed

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("email");
    options.Scope.Add("profile");

    options.GetClaimsFromUserInfoEndpoint = true;
    options.ClaimActions.MapJsonKey("groups", "cognito:groups");
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("WarehouseUser", policy =>
    {
        policy.RequireAssertion(context =>
        {
            var user = context.User;
            Console.WriteLine("=== Authorization Check ===");
            Console.WriteLine($"Looking for claim: cognito:groups = Warehouse");

            foreach (var claim in user.Claims)
            {
                Console.WriteLine($"Found claim: {claim.Type} = {claim.Value}");
                if (claim.Type == "cognito:groups" && claim.Value == "Warehouse")
                {
                    Console.WriteLine("✅ MATCH FOUND!");
                    return true;
                }
            }
            Console.WriteLine("❌ NO MATCH FOUND");
            return false;
        });
    });

    options.AddPolicy("CustomerUser", policy =>
    {
        policy.RequireAssertion(context =>
            context.User.HasClaim("cognito:groups", "Customer")
        );
    });
});

// Add EF Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IGameService, GameService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Enables support for forwarded headers in reverse proxy scenarios to correctly
// determine the original client IP and protocol (HTTP/HTTPS).
// I am using AWS which is using Nginx for Reverse Proxy, and I believe
// Nginx is terminating https and forwarding requests to my app over http.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); 
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapStaticAssets();

app.Run();