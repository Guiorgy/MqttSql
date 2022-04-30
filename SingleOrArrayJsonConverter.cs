using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MqttSql
{
    public sealed class SingleOrArrayJsonConverter : JsonConverterFactory
	{
		private JsonConverterFactory? _jsonConverterFactory = null;

		private JsonConverterFactory GetJsonConverterFactory(Type typeToConvert)
		{
			if (_jsonConverterFactory == null)
			{
				ArgumentNullException.ThrowIfNull(typeToConvert, nameof(typeToConvert));
				if (!typeToConvert.IsArray) throw new ArgumentException($"{nameof(typeToConvert)} must be an Array!");
				Type valueType = typeToConvert.GetElementType()!;
				_jsonConverterFactory =
					(JsonConverterFactory)Activator.CreateInstance(
						typeof(SingleOrArrayJsonConverter<>).MakeGenericType(valueType),
						BindingFlags.Instance | BindingFlags.Public,
						binder: null,
						args: null,
						culture: null)!;
			}
			return _jsonConverterFactory;
		}

		public override bool CanConvert(Type typeToConvert)
		{
			if (typeToConvert.IsArray)
				return GetJsonConverterFactory(typeToConvert).CanConvert(typeToConvert);
			else
				return false;
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return GetJsonConverterFactory(typeToConvert).CreateConverter(typeToConvert, options)!;
		}
	}

	public sealed class SingleOrArrayJsonConverter<TValue> : JsonConverterFactory
	{
		public override bool CanConvert(Type typeToConvert)
		{
			return typeToConvert == typeof(TValue[]);
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return (JsonConverter)Activator.CreateInstance(
				typeof(SingleOrArrayJsonConverterInner),
				BindingFlags.Instance | BindingFlags.Public,
				binder: null,
				args: new object[] { options },
				culture: null)!;
		}

		private sealed class SingleOrArrayJsonConverterInner : JsonConverter<TValue[]>
		{
			private readonly Type _singleType;
			private readonly JsonConverter<TValue> _singleConverter;
			private readonly Type _arrayType;
			private readonly JsonConverter<TValue[]> _arrayConverter;

#pragma warning disable S1144 // Unused private types or members should be removed
            public SingleOrArrayJsonConverterInner(JsonSerializerOptions options)
			{
				_singleType = typeof(TValue);
				_singleConverter = (JsonConverter<TValue>)options.GetConverter(_singleType);
				_arrayType = typeof(TValue[]);
				_arrayConverter = (JsonConverter<TValue[]>)options.GetConverter(_arrayType);
			}
#pragma warning restore S1144 // Unused private types or members should be removed

            public override TValue[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
                if (reader.TokenType == JsonTokenType.StartArray)
				{
                    return _arrayConverter.Read(ref reader, _arrayType, options);
				}
				else
				{
					var value = _singleConverter.Read(ref reader, _singleType, options);
					if (value != null) return new TValue[1] { value };
					return default;
				}
			}

			public override void Write(Utf8JsonWriter writer, TValue[] values, JsonSerializerOptions options)
			{
				_arrayConverter.Write(writer, values, options);
			}
		}
	}

	public sealed class SingleOrArrayJsonConverter<TValue, TJsonConverter> : JsonConverterFactory where TJsonConverter : JsonConverter<TValue>
	{
		public override bool CanConvert(Type typeToConvert)
		{
			return typeToConvert == typeof(TValue[]);
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return (JsonConverter)Activator.CreateInstance(
				typeof(SingleOrArrayJsonConverterInner),
				BindingFlags.Instance | BindingFlags.Public,
				binder: null,
				args: null,
				culture: null)!;
		}

		private sealed class SingleOrArrayJsonConverterInner : JsonConverter<TValue[]>
		{
			private readonly Type _singleType;
			private readonly JsonConverter<TValue> _singleConverter;
			private readonly Type _arrayType;
			private readonly JsonConverter<TValue[]> _arrayConverter;

#pragma warning disable S1144 // Unused private types or members should be removed
			public SingleOrArrayJsonConverterInner()
			{
				_singleType = typeof(TValue);
				_singleConverter =
					(JsonConverter<TValue>)Activator.CreateInstance(
						typeof(TJsonConverter),
						BindingFlags.Instance | BindingFlags.Public,
						binder: null,
						args: null,
						culture: null)!;
				_arrayType = typeof(TValue[]);
				_arrayConverter = new ArrayJsonConverter(_singleConverter);
			}
#pragma warning restore S1144 // Unused private types or members should be removed

			public override TValue[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				if (reader.TokenType == JsonTokenType.StartArray)
				{
					return _arrayConverter.Read(ref reader, _arrayType, options);
				}
				else
				{
					var value = _singleConverter.Read(ref reader, _singleType, options);
					if (value != null) return new TValue[1] { value };
					return default;
				}
			}

			public override void Write(Utf8JsonWriter writer, TValue[] values, JsonSerializerOptions options)
			{
				_arrayConverter.Write(writer, values, options);
			}

			public sealed class ArrayJsonConverter : JsonConverter<TValue[]>
			{
				private readonly Type _valueType;
				private readonly JsonConverter<TValue> _valueConverter;

				public ArrayJsonConverter(JsonConverter<TValue> valueConverter)
				{
					_valueType = typeof(TValue);
					_valueConverter = valueConverter;
				}

				public override TValue[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
				{
					if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException();

					List<TValue> values = new();
					while (reader.Read())
					{
						if (reader.TokenType == JsonTokenType.EndArray) break;

						var value = _valueConverter.Read(ref reader, _valueType, options);
						if (value != null) values.Add(value);
					}
					return values.ToArray();
				}

				public override void Write(Utf8JsonWriter writer, TValue[] values, JsonSerializerOptions options)
				{
					writer.WriteStartArray();
					foreach (TValue value in values)
						_valueConverter.Write(writer, value, options);
					writer.WriteEndArray();
				}
			}
		}
	}
}
