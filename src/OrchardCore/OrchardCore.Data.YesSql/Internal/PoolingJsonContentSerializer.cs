using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using Cysharp.Text;
using Newtonsoft.Json;
using YesSql;

namespace OrchardCore.Data.YesSql.Internal
{
    /// <summary>
    /// Custom YesSql content serializer which forwards to generic pooling JSON serializer with custom settings.
    /// </summary>
    internal sealed class PoolingJsonContentSerializer : IContentSerializer
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore,
            CheckAdditionalContent = false
        };

        private readonly JsonArrayPool<char> _arrayPool;

        public PoolingJsonContentSerializer(ArrayPool<char> arrayPool)
        {
            _arrayPool = new JsonArrayPool<char>(arrayPool);
        }

        public object Deserialize(string content, Type type)
        {
            var jsonSerializer = JsonSerializer.CreateDefault(_jsonSettings);
            using var reader = new JsonTextReader(new StringReader(content))
            {
                ArrayPool = _arrayPool
            };
            return jsonSerializer.Deserialize(reader, type);
        }

        public dynamic DeserializeDynamic(string content) => Deserialize(content, null);

        public string Serialize(object item)
        {
            var jsonSerializer = JsonSerializer.CreateDefault(_jsonSettings);
            using var sw = new ZStringWriter(CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(sw)
            {
                ArrayPool = _arrayPool,
                Formatting = jsonSerializer.Formatting
            };
            jsonSerializer.Serialize(jsonWriter, item, null);
            return sw.ToString();
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
