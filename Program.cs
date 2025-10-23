using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using SPEAgentWithRetrieval.Models;
using SPEAgentWithRetrieval.Services;
using SPEAgentWithRetrieval.Middleware;

namespace SPEAgentWithRetrieval;

class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure forwarded headers for Azure Container Apps
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                                      ForwardedHeaders.XForwardedProto | 
                                      ForwardedHeaders.XForwardedHost;
            options.RequireHeaderSymmetry = false;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
            
            // Trust all proxies for Azure Container Apps
            options.ForwardLimit = null;
        });

        // Configure additional HTTPS settings for Azure Container Apps
        builder.Services.Configure<HttpsRedirectionOptions>(options =>
        {
            options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
            options.HttpsPort = 443;
        });

        // Build configuration
        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Add Microsoft Identity platform authentication
        builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "AzureAd")
            .EnableTokenAcquisitionToCallDownstreamApi(new[] {
                "https://graph.microsoft.com/Files.Read.All",
                "https://graph.microsoft.com/Sites.Read.All",
                "https://graph.microsoft.com/Mail.Send",
                "https://graph.microsoft.com/User.Read.All"
            })
            .AddInMemoryTokenCaches();

        // Configure OpenID Connect for Azure Container Apps - AFTER Microsoft Identity setup
        builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            var redirectUri = builder.Configuration["AzureAd:RedirectUri"];
            
            if (!builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(redirectUri))
            {
                // Force HTTPS metadata
                options.RequireHttpsMetadata = true;
                
                // Add the redirect URI to the configuration section instead
                var azureAdSection = builder.Configuration.GetSection("AzureAd");
                azureAdSection["RedirectUri"] = redirectUri;
            }
            
            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    // Force HTTPS redirect URI
                    if (!builder.Environment.IsDevelopment())
                    {
                        if (!string.IsNullOrEmpty(redirectUri))
                        {
                            context.Properties.RedirectUri = redirectUri;
                        }
                        else
                        {
                            // Fallback: construct HTTPS URI
                            var host = context.Request.Host.Value;
                            context.Properties.RedirectUri = $"https://{host}/signin-oidc";
                        }
                        
                        // Log for debugging
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogWarning("REDIRECT URI DEBUG: Using redirect URI: {RedirectUri}", context.Properties.RedirectUri);
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(context.Exception, "Authentication failed: {ErrorMessage}", context.Exception.Message);
                    
                    if (context.Exception.Message.Contains("redirect_uri"))
                    {
                        logger.LogError("REDIRECT URI MISMATCH: Expected HTTPS but got HTTP redirect URI");
                        logger.LogError("Configure this in Azure AD: https://ca-speagentwith-sdjs4rkwdwu3w.livelyrock-fbb72131.eastus2.azurecontainerapps.io/signin-oidc");
                    }
                    
                    return Task.CompletedTask;
                }
            };
        });

        // Configure options
        builder.Services.Configure<AzureAIFoundryOptions>(builder.Configuration.GetSection("AzureAIFoundry"));
        builder.Services.Configure<Microsoft365Options>(builder.Configuration.GetSection("Microsoft365"));
        builder.Services.Configure<ChatSettingsOptions>(builder.Configuration.GetSection("ChatSettings"));

        // Register services
        builder.Services.AddScoped<IRetrievalService, CopilotRetrievalService>();
        builder.Services.AddScoped<IFoundryService, FoundryService>();
        builder.Services.AddScoped<IChatService, ChatService>();
        builder.Services.AddScoped<IMailService, GraphMailService>();

        // Add web services - Remove global authorization requirement
        builder.Services.AddControllersWithViews()
            .AddMicrosoftIdentityUI();

        builder.Services.AddRazorPages();

        // Add logging
        builder.Services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var app = builder.Build();

        // Apply forwarded headers before any other middleware
        app.UseForwardedHeaders();

        // Add custom HTTPS redirect middleware for authentication
        app.UseMiddleware<HttpsRedirectMiddleware>();

        // Set environment variables for HTTPS forcing in production
        if (!app.Environment.IsDevelopment())
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true");
        }

        // Configure the HTTP request pipeline
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();

        app.Run();
    }
}
