using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.JsonConverters;

public class IntToStringConverter {
    public class IntToStringJsonConverter : JsonConverter<int> {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetString() != null ? int.Parse(reader.GetString()) : 0;

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}