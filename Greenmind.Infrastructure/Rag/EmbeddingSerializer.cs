using System.Text.Json;

namespace Greenmind.Infrastructure.Rag
{
    public static class EmbeddingSerializer
    {
        public static string Serialize(ReadOnlyMemory<float> embedding) =>
            JsonSerializer.Serialize(embedding.ToArray());

        public static float[] Deserialize(string embeddingJson) =>
            JsonSerializer.Deserialize<float[]>(embeddingJson)
            ?? throw new InvalidOperationException("Stored embedding could not be deserialized.");
    }
}