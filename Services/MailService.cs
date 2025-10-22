using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using SPEAgentWithRetrieval.Models;

namespace SPEAgentWithRetrieval.Services;

public class GraphMailService : IMailService
{
  private readonly GraphServiceClient _graphClient;
  private readonly Microsoft365Options _microsoft365Options;
  private readonly ILogger<GraphMailService> _logger;
  private readonly Azure.Core.TokenCredential _credential;

  public GraphMailService(
    IOptions<Microsoft365Options> microsoft365Options,
    ILogger<GraphMailService> logger)
  {
    _microsoft365Options = microsoft365Options.Value;
    _logger = logger;
    // Use Interactive Browser Authentication for user context - store as field for reuse
    _credential = _microsoft365Options.UseUserAuthentication
      ? new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
      {
        TenantId = _microsoft365Options.TenantId,
        ClientId = _microsoft365Options.ClientId,
        RedirectUri = new Uri("http://localhost")
      })
      : new DefaultAzureCredential();
    _graphClient = new GraphServiceClient(_credential, _microsoft365Options.Scopes);
  }

  public async Task<string> RetrieveUsersEmail(string recipient)
  {
    try
    {
      var userResponse = await _graphClient.Users.GetAsync((requestConfiguration) =>
      {
        requestConfiguration.Headers.Add("ConsistencyLevel", new string[] { "eventual" });
        requestConfiguration.QueryParameters.Search = $"\"displayName:{recipient}\"";
        requestConfiguration.QueryParameters.Select = new string[] { "Mail", "id" };
      });

      // var userResponse = await _graphClient.Users.GetAsync();
      var user = userResponse?.Value?.FirstOrDefault();
      return user.Mail;
    }
    catch (Exception e)
    {
      _logger.LogError($"Failed to resolve {recipient}'s email");
    }
      return "test";
    }
  public async Task SendMailAsync(string recipient, string emailContent, string fileName, CancellationToken cancellationToken = default)
  {
    try
    {
      var userEmail = await this.RetrieveUsersEmail(recipient);

      var requestBody = new SendMailPostRequestBody
      {
        Message = new Message
        {
          Subject = $"Compliance report for file {fileName}",
          Body = new ItemBody
          {
            ContentType = BodyType.Text,
            Content = emailContent,
          },
          ToRecipients = new List<Recipient>
          {
            new Recipient
            {
              EmailAddress = new EmailAddress
              {
                Address = recipient,
              },
            },
          }
        },
        SaveToSentItems = false,
      };
      await _graphClient.Me.SendMail.PostAsync(requestBody);
    }
    catch (Exception exception)
    {
      _logger.LogError(exception, "Error occurred whilst sending email with findings");
    }
  }
}