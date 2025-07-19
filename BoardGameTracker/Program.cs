using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using BoardGameTracker.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();

// ----- Add Authentication for Cognito
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Domain = ".SwampDragonInn.com";
    options.Cookie.Name = "CompanyAuth";
    options.LoginPath = "/authentication/challenge";
    options.LogoutPath = "/authentication/signout";
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    // 1. Your app trusts Cognito as the Identity Provider
    options.Authority = builder.Configuration["Cognito:Authority"];

    // Cognito is both the Identity Provider AND Identity Source
    // Users are stored in Cognito User Pool (source)
    // Cognito authenticates them and issues tokens (provider)

    // 2. Protocol specifications: OpenID Connect
    // OAuth 2.0 "Authorization Code" flow
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("email");
    options.Scope.Add("profile");
    // 3. These are your app's credentials with Cognito:
    options.ClientId = builder.Configuration["Cognito:ClientId"];
    options.ClientSecret = builder.Configuration["Cognito:ClientSecret"];
    
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    
    // Map Cognito groups to claims
    options.ClaimActions.MapJsonKey("groups", "cognito:groups");
    
    options.Events = new OpenIdConnectEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"Token validated for user: {context.Principal.Identity.Name}");
            Console.WriteLine("All claims:");
            foreach (var claim in context.Principal.Claims)
            {
                Console.WriteLine($"  {claim.Type}: {claim.Value}");
            }
            return Task.CompletedTask;
        }
    };
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
// ----- End Authentication

// Add EF Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Service
builder.Services.AddScoped<IGameService, GameService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
