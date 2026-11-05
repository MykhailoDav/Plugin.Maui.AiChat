using System.ClientModel;
using System.Text.Json;
using Anthropic;
using Google.GenAI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using Plugin.Maui.AiChat.Tests.TestHelpers;

namespace Plugin.Maui.AiChat.Tests.Docs;

/// <summary>
/// The C# samples of README.md and llms-full.txt, copied here so they keep compiling. The ones that need no network
/// also run against a scripted client.
/// </summary>
public class DocSamplesTests
{
    // README: Quick start.
    public sealed class ChatViewModel
    {
        public ChatViewModel(string apiKey)
        {
            IChatClient client = new OpenAIClient(apiKey).GetChatClient("gpt-5-mini").AsIChatClient();
            Chat = new AiChatSession(client)
            {
                Options = new ChatOptions { Instructions = "You are a helpful assistant. Answer in Markdown." },
            };
        }

        public AiChatSession Chat { get; }
    }

    // llms-full.txt: Minimal chat.
    public sealed class MinimalChatViewModel(IChatClient client)
    {
        public AiChatSession Chat { get; } = new(client)
        {
            Options = new ChatOptions { Instructions = "You are a helpful assistant." },
        };
    }

    [Fact]
    public void Quick_start_creates_a_session()
    {
        var viewModel = new ChatViewModel("sk-test");

        Assert.Equal("You are a helpful assistant. Answer in Markdown.", viewModel.Chat.Options!.Instructions);
        Assert.NotNull(new MinimalChatViewModel(new ScriptedChatClient()).Chat.Options);
    }

    [Fact]
    public void Other_models()
    {
        const string apiKey = "test-key";

        // README: Other models. The Apple Intelligence client is iOS-only, so it is not compiled here.
        IChatClient claude = new AnthropicClient { ApiKey = apiKey }.AsIChatClient("claude-sonnet-5-5");
        IChatClient gemini = new Client(apiKey: apiKey).AsIChatClient("gemini-3.6-flash");
        IChatClient ollama = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");
        IChatClient compatible = new OpenAIClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri("https://openrouter.ai/api/v1") })
            .GetChatClient("openai/gpt-5-mini")
            .AsIChatClient();

        Assert.All([claude, gemini, ollama, compatible], client => Assert.NotNull(new AiChatSession(client)));
    }

    [Fact]
    public void Tools_and_approval()
    {
        const string apiKey = "sk-test";

        // README: Tools and approval.
        IChatClient client = new OpenAIClient(apiKey).GetChatClient("gpt-5-mini").AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        var chat = new AiChatSession(client)
        {
            Options = new ChatOptions
            {
                Tools =
                [
                    AIFunctionFactory.Create(() => DateTimeOffset.Now, "get_time", "Gets the local date and time."),
                    new ApprovalRequiredAIFunction(
                        AIFunctionFactory.Create((string path) => DeleteFile(path), "delete_file", "Deletes a file.")),
                ],
            },
        };

        Assert.Equal(2, chat.Options.Tools!.Count);
    }

    private static bool DeleteFile(string path) => path.Length > 0;

    [Fact]
    public async Task Saving_the_conversation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aichat-{Guid.NewGuid():N}.json");
        var chat = new AiChatSession(new ScriptedChatClient().RespondText("Hello"));
        try
        {
            // README: Saving the conversation.
            var typeInfo = AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(IList<ChatMessage>));

            chat.ResponseCompleted += (_, _) =>
                File.WriteAllText(path, JsonSerializer.Serialize(chat.Messages.ToList(), typeInfo));

            if (File.Exists(path))
            {
                chat.Load((IList<ChatMessage>)JsonSerializer.Deserialize(File.ReadAllText(path), typeInfo)!);
            }

            await chat.SendAsync("Hi");
            var restored = new AiChatSession(new ScriptedChatClient());
            restored.Load((IList<ChatMessage>)JsonSerializer.Deserialize(File.ReadAllText(path), typeInfo)!);

            Assert.Equal(["Hi", "Hello"], restored.Messages.Select(m => m.Text));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
