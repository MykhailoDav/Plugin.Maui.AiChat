using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Plugin.Maui.AiChat.Sample.Services;

/// <summary>
/// Saves the conversation to a file and loads it at start. The history is MEAI's own messages, so
/// <see cref="AIJsonUtilities.DefaultOptions"/> serializes it, tool calls and approvals included, without reflection.
/// </summary>
public sealed class ChatStore(IFileSystem fileSystem)
{
    private static readonly JsonTypeInfo<IList<ChatMessage>> TypeInfo =
        (JsonTypeInfo<IList<ChatMessage>>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(IList<ChatMessage>));

    private string FilePath => Path.Combine(fileSystem.AppDataDirectory, "conversation.json");

    public IList<ChatMessage> Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize(File.ReadAllText(FilePath), TypeInfo) ?? [] : [];
        }
        catch (JsonException)
        {
            // A file from an older format is not worth a crash.
            return [];
        }
    }

    public void Save(IEnumerable<ChatMessage> messages) => File.WriteAllText(FilePath, JsonSerializer.Serialize(messages.ToList(), TypeInfo));

    public void Delete() => File.Delete(FilePath);
}
