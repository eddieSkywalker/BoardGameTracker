using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using BoardGameTracker.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddControllers();

// ----- Add Authentication for Cognito
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = builder.Configuration["Cognito:Authority"];
    options.ClientId = builder.Configuration["Cognito:ClientId"];
    options.ClientSecret = builder.Configuration["Cognito:ClientSecret"];
    options.ResponseType = OpenIdConnectResponseType.Code;
    
    // Clear default scopes and add only what Cognito supports
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("email");
    options.Scope.Add("profile");
    
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
