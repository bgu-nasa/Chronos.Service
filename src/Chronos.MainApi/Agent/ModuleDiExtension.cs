using Chronos.Agent;
using Chronos.Agent.Conversation;
using Chronos.Agent.Extraction;
using Chronos.MainApi.Agent.Services;

namespace Chronos.MainApi.Agent;

public static class ModuleDiExtension
{
    public static void AddAgentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmAdapterSettings>(configuration.GetSection("Agent:Llm"));
        services.AddHttpClient();

        services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        services.AddSingleton<AgentStateMachine>();
        services.AddSingleton<RuleBasedConstraintExtractor>();
        services.AddSingleton<ILlmAdapter, OpenAiCompatibleLlmAdapter>();
        services.AddSingleton<IConstraintExtractor, LlmConstraintExtractor>();
        services.AddSingleton<ConstraintValidator>();
        services.AddScoped<AgentOrchestrator>();

        services.AddScoped<IAgentService, AgentService>();
    }
}
