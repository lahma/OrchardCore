using System.Buffers;
using System.Globalization;
using System.IO;
using Cysharp.Text;
using Newtonsoft.Json;

namespace OrchardCore.DynamicCache.Internal
{
    /// <summary>
    /// Handles JSON.NET serialization utilizing pooled array buffers and string builders.
    /// </summary>
    internal sealed class PoolingJsonSerializer
    {
        private readonly JsonArrayPool<char> _arrayPool;

        public PoolingJsonSerializer(ArrayPool<char> arrayPool)
        {
            _arrayPool = new JsonArrayPool<char>(arrayPool);
        }

        internal string Serialize(object item)
        {
            var jsonSerializer = JsonSerializer.CreateDefault();
            using var sw = new ZStringWriter(CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(sw)
            {
                ArrayPool = _arrayPool,
                Formatting = jsonSerializer.Formatting
            };
            jsonSerializer.Serialize(jsonWriter, item, null);
            return sw.ToString();
        }

        internal T Deserialize<T>(string content)
        {
            var jsonSerializer = JsonSerializer.CreateDefault();
            using var reader = new JsonTextReader(new StringReader(content))
            {
                ArrayPool = _arrayPool
            };
            return jsonSerializer.Deserialize<T>(reader);
        }

        private class JsonArrayPool<T> : IArrayPool<T>
        {
            private readonly ArrayPool<T> _inner;

            public JsonArrayPool(ArrayPool<T> inner) => _inner = inner;

            public T[] Rent(int minimumLength) => _inner.Rent(minimumLength);

            public void Return(T[] array) => _inner.Return(array);
        }
    }
}
