namespace Greenmind.Infrastructure.Rag
{
    public static class VectorMath
    {
        /// <summary>
        /// Cosine similarity between two equal-length embedding vectors.
        /// Returns a value in [-1, 1]; for normalized embeddings this is effectively [0, 1].
        /// </summary>
        public static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector length mismatch: {a.Length} vs {b.Length}");

            double dot = 0, magA = 0, magB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }

            if (magA == 0 || magB == 0) return 0;

            return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
        }
    }
}