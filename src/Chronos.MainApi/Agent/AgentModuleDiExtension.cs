using Chronos.Agent;
using Chronos.Agent.Configuration;
using Chronos.Agent.Conversation;
using Chronos.Agent.Extraction;
using Chronos.Agent.Submission;
using Chronos.MainApi.Schedule.Services;

namespace Chronos.MainApi.Agent;

public static class AgentModuleDiExtension
{
    public static void AddAgentModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<PuterOptions>(configuration.GetSection(PuterOptions.SectionName));

        // Conversation store (in-memory, thread-safe — POC only)
        services.AddSingleton<IConversationStore, InMemoryConversationStore>();

        // LLM adapter — selected by config
        var provider = configuration.GetValue<string>("Agent:LlmProvider") ?? "ollama";

        if (provider.Equals("puter", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ILlmAdapter, PuterLlmAdapter>((sp, client) => { })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());

            services.AddScoped<ILlmAdapter>(sp =>
            {
                var options = configuration.GetSection(PuterOptions.SectionName).Get<PuterOptions>() ?? new PuterOptions();
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient(nameof(PuterLlmAdapter));
                return new PuterLlmAdapter(client, options);
            });
        }
        else
        {
            // Ollama with self-signed cert bypass (university server)
            services.AddHttpClient("OllamaClient")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                });

            services.AddScoped<ILlmAdapter>(sp =>
            {
                var options = configuration.GetSection(OllamaOptions.SectionName).Get<OllamaOptions>() ?? new OllamaOptions();
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient("OllamaClient");
                return new OllamaLlmAdapter(client, options);
            });
        }

        // Extraction
        services.AddScoped<ConstraintExtractor>();

        // Service adapters (bridge existing Chronos services → agent interfaces)
        services.AddScoped<IAgentConstraintService>(sp =>
            new UserConstraintServiceAdapter(sp.GetRequiredService<IUserConstraintService>()));
        services.AddScoped<IAgentPreferenceService>(sp =>
            new UserPreferenceServiceAdapter(sp.GetRequiredService<IUserPreferenceService>()));

        // Submission
        services.AddScoped<IAgentSubmitter, ServiceBackedSubmitter>();

        // Orchestrator
        services.AddScoped<AgentOrchestrator>();
    }
}
