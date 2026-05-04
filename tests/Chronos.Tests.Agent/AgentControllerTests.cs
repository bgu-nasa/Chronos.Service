using Chronos.Agent;
using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;
using Chronos.Agent.Extraction;
using Chronos.Agent.Submission;
using Chronos.MainApi.Agent;
using Chronos.MainApi.Agent.Contracts;
using Chronos.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace Chronos.Tests.Agent;

public class AgentControllerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _periodId = Guid.NewGuid();

    private readonly Mock<ILlmAdapter> _llmAdapter = new();
    private readonly Mock<IAgentSubmitter> _submitter = new();
    private readonly InMemoryConversationStore _store = new();
    private readonly Mock<ILogger<AgentController>> _logger = new();

    private AgentController CreateController()
    {
        var extractor = new ConstraintExtractor(_llmAdapter.Object);
        var orchestrator = new AgentOrchestrator(_llmAdapter.Object, extractor, _submitter.Object, _store);

        var controller = new AgentController(_logger.Object, orchestrator, _store);

        // Setup HttpContext with auth claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Items["organizationId"] = _orgId.ToString();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private void SetupLlm(string conversationReply = "Got it!")
    {
        _llmAdapter
            .Setup(a => a.ChatAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.Is<LlmOptions?>(o => o == null || !o.JsonMode),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(conversationReply));

        var json = """{"hardConstraints":[{"key":"avoid_weekday","value":"Friday"}],"softPreferences":[{"key":"preferred_weekday","value":"Monday"}]}""";
        _llmAdapter
            .Setup(a => a.ChatAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.Is<LlmOptions?>(o => o != null && o.JsonMode),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(json));

        _submitter
            .Setup(s => s.SubmitAsync(It.IsAny<ConstraintProposal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task StartSession_ReturnsCreated_WithSessionId()
    {
        SetupLlm();
        var controller = CreateController();

        var result = await controller.StartSession(new StartSessionRequest(_periodId));

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(created.Value);
    }

    [Fact]
    public async Task SendMessage_ReturnsOk_WithAgentResponse()
    {
        SetupLlm("I understand you want to avoid Fridays.");
        var controller = CreateController();

        // Start session first
        var startResult = await controller.StartSession(new StartSessionRequest(_periodId)) as CreatedAtActionResult;
        var sessionId = (Guid)startResult!.RouteValues!["sessionId"]!;

        var result = await controller.SendMessage(sessionId, new SendMessageRequest("I can't work Fridays"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AgentSessionResponse>(ok.Value);
        Assert.Equal(AgentState.Discovery, response.State);
        Assert.Equal("I understand you want to avoid Fridays.", response.AssistantMessage);
    }

    [Fact]
    public async Task FullFlow_StartThroughApprove_Works()
    {
        SetupLlm();
        var controller = CreateController();

        // Start
        var startResult = await controller.StartSession(new StartSessionRequest(_periodId)) as CreatedAtActionResult;
        var sessionId = (Guid)startResult!.RouteValues!["sessionId"]!;

        // Message
        await controller.SendMessage(sessionId, new SendMessageRequest("I can't work Fridays and prefer Mondays"));

        // Submit
        var submitResult = await controller.RequestSubmit(sessionId) as OkObjectResult;
        var submitResponse = Assert.IsType<AgentSessionResponse>(submitResult!.Value);
        Assert.Equal(AgentState.Submit, submitResponse.State);
        Assert.NotNull(submitResponse.Draft);
        Assert.Single(submitResponse.Draft!.HardConstraints);

        // Approve
        var approveResult = await controller.Approve(sessionId) as OkObjectResult;
        var approveResponse = Assert.IsType<AgentSessionResponse>(approveResult!.Value);
        Assert.Equal(AgentState.Approved, approveResponse.State);

        _submitter.Verify(
            s => s.SubmitAsync(It.IsAny<ConstraintProposal>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_UnknownSession_ThrowsNotFoundException()
    {
        SetupLlm();
        var controller = CreateController();

        await Assert.ThrowsAsync<NotFoundException>(
            () => controller.SendMessage(Guid.NewGuid(), new SendMessageRequest("test")));
    }

    [Fact]
    public async Task Approve_NotInSubmitState_ThrowsBadRequest()
    {
        SetupLlm();
        var controller = CreateController();

        var startResult = await controller.StartSession(new StartSessionRequest(_periodId)) as CreatedAtActionResult;
        var sessionId = (Guid)startResult!.RouteValues!["sessionId"]!;

        await Assert.ThrowsAsync<BadRequestException>(
            () => controller.Approve(sessionId));
    }

    [Fact]
    public async Task SendMessage_WrongUser_ThrowsUnauthorized()
    {
        SetupLlm();
        var controller = CreateController();

        // Start session with this user
        var startResult = await controller.StartSession(new StartSessionRequest(_periodId)) as CreatedAtActionResult;
        var sessionId = (Guid)startResult!.RouteValues!["sessionId"]!;

        // Switch to a different user
        var otherUserId = Guid.NewGuid();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, otherUserId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => controller.SendMessage(sessionId, new SendMessageRequest("test")));
    }

    [Fact]
    public async Task Revise_AfterSubmit_TransitionsToRevision()
    {
        SetupLlm();
        var controller = CreateController();

        var startResult = await controller.StartSession(new StartSessionRequest(_periodId)) as CreatedAtActionResult;
        var sessionId = (Guid)startResult!.RouteValues!["sessionId"]!;

        await controller.SendMessage(sessionId, new SendMessageRequest("I can't work Fridays"));
        await controller.RequestSubmit(sessionId);

        var result = await controller.Revise(sessionId) as OkObjectResult;
        var response = Assert.IsType<AgentSessionResponse>(result!.Value);
        Assert.Equal(AgentState.Revision, response.State);
    }
}
