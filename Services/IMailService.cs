namespace SPEAgentWithRetrieval.Services;

public interface IMailService
{
  Task SendMailAsync(
    string recipient,
    string emailContent,
    string fileName,
    CancellationToken cancellationToken = default
  );
}