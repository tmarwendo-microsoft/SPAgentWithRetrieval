using Microsoft.AspNetCore.Authentication;

namespace SPEAgentWithRetrieval.Middleware
{
    public class HttpsRedirectMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<HttpsRedirectMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public HttpsRedirectMiddleware(RequestDelegate next, ILogger<HttpsRedirectMiddleware> logger, IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Force HTTPS for authentication redirects in production
            if (!_environment.IsDevelopment() && 
                context.Request.Path.StartsWithSegments("/signin-oidc") || 
                context.Request.Path.StartsWithSegments("/signout-callback-oidc"))
            {
                // Ensure we're using HTTPS for authentication callbacks
                if (context.Request.Scheme == "http")
                {
                    var httpsUrl = $"https://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
                    _logger.LogWarning("Redirecting authentication callback from HTTP to HTTPS: {HttpsUrl}", httpsUrl);
                    context.Response.Redirect(httpsUrl, permanent: true);
                    return;
                }
            }

            await _next(context);
        }
    }
}