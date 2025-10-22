using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SPEAgentWithRetrieval.Models;
using SPEAgentWithRetrieval.Services;

namespace SPEAgentWithRetrieval;

class Program
{
    static async Task Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Build host with dependency injection
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configure options
                services.Configure<AzureAIFoundryOptions>(configuration.GetSection("AzureAIFoundry"));
                services.Configure<Microsoft365Options>(configuration.GetSection("Microsoft365"));
                services.Configure<ChatSettingsOptions>(configuration.GetSection("ChatSettings"));

                // Register services
                services.AddScoped<IRetrievalService, CopilotRetrievalService>();
                services.AddScoped<IFoundryService, FoundryService>();
                services.AddScoped<IChatService, ChatService>();
                services.AddScoped<IMailService, GraphMailService>();

                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        // Get the chat service and logger
        var chatService = host.Services.GetRequiredService<IChatService>();
        var mailService = host.Services.GetRequiredService<IMailService>();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Azure AI Chat Agent with SharePoint RAG started");
        
        Console.WriteLine("=== Azure AI Chat Agent with SharePoint RAG ===");
        Console.WriteLine("Ask questions about your Microsoft 365 content!");
        Console.WriteLine("Type 'exit' or 'quit' to end the conversation.");
        Console.WriteLine("Type 'clear' to clear the console.");
        Console.WriteLine();

        // Main chat loop
        while (true)
        {
            Console.Write("You: Should I run the compliance agent ? y/n. Type exit or quit to terminate ");
            var userInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userInput))
            {
                continue;
            }

            if (userInput.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // var test = mailService.SendMailAsync("Takudzwa Marwendo", "test", "test");
                    // Process the chat request
                    // string filesContentQuery = $"What are the policies listed in {userInput}?";
                    // string rulesQuery = $"What are the rules that apply to {userInput}?";

                    var chatRequest = new ChatRequest { FileName = userInput };
                    var response = await chatService.ProcessChatAsync(chatRequest);

                    Console.WriteLine($"Assistant: {response.LlmResponse}");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in chat loop");
                    Console.WriteLine("Sorry, I encountered an error. Please try again.");
                    Console.WriteLine();
                }
            }
            
            // Handle special commands
            if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase) || 
                userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            if (userInput.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                Console.Clear();
                Console.WriteLine("=== Azure AI Chat Agent with SharePoint RAG ===");
                continue;
            }
        }

        await host.StopAsync();
    }
}
