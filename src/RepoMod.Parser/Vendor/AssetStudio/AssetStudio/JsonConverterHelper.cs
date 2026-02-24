using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetStudio
{
    public static partial class JsonConverterHelper
    {
        public static SerializedFile AssetsFile { get; set; }

        public class FloatConverter : JsonConverter<float>
        {
            public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return JsonSerializer.Deserialize<float>(ref reader, new JsonSerializerOptions
                {
                    NumberHandling = options.NumberHandling
                });
            }

            public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    if (options.NumberHandling == JsonNumberHandling.AllowNamedFloatingPointLiterals)
                    {
                        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        writer.WriteStringValue(JsonSerializer.Serialize(value));
                    }
                }
                else
                {
                    writer.WriteNumberValue((decimal)value + 0.0m);
                }
            }
        }

        public class ByteArrayConverter : JsonConverter<byte[]>
        {
            public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return reader.TokenType == JsonTokenType.StartArray
                    ? JsonSerializer.Deserialize<List<byte>>(ref reader).ToArray()
                    : JsonSerializer.Deserialize<byte[]>(ref reader);
            }

            public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
            {
                writer.WriteBase64StringValue(value);
            }
        }

        public class KVPConverter : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);
            }

            public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
            {
                var kvpArgs = type.GetGenericArguments();
                return (JsonConverter)Activator.CreateInstance(typeof(KVPConverterInternal<,>).MakeGenericType(kvpArgs));
            }
        }

        private class KVPConverterInternal<TKey, TValue> : JsonConverter<KeyValuePair<TKey, TValue>>
        {
            public override KeyValuePair<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                reader.Read();
                reader.Read();
                var key = reader.TokenType == JsonTokenType.StartObject
                    ? JsonSerializer.Deserialize<Dictionary<string, TKey>>(ref reader).Values.First()
                    : JsonSerializer.Deserialize<TKey>(ref reader);
                reader.Read();
                reader.Read();
                var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                reader.Read();
                return new KeyValuePair<TKey, TValue>(key, value);
            }

            public override void Write(Utf8JsonWriter writer, KeyValuePair<TKey, TValue> value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        public class PPtrConverter : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(PPtr<>);
            }

            public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
            {
                var elementType = type.GetGenericArguments()[0];
                return (JsonConverter)Activator.CreateInstance(typeof(PPtrConverterInternal<>).MakeGenericType(elementType));
            }
        }

        private class PPtrConverterInternal<T> : JsonConverter<PPtr<T>> where T : Object
        {
            public override PPtr<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var pptrObj = JsonSerializer.Deserialize<PPtr<T>>(ref reader, new JsonSerializerOptions { IncludeFields = true });
                pptrObj.AssetsFile = AssetsFile;
                return pptrObj;
            }

            public override void Write(Utf8JsonWriter writer, PPtr<T> value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
    }
}
