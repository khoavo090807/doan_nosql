using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace doan_cuoiky_nosql.Models;

/// <summary>
/// Handles NgaySinh stored as either a native BSON date or Extended JSON { "$date": "..." }.
/// </summary>
public class FlexibleDateTimeSerializer : SerializerBase<DateTime?>
{
    public override DateTime? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        var bsonType = reader.GetCurrentBsonType();

        if (bsonType == BsonType.Null) { reader.ReadNull(); return null; }
        if (bsonType == BsonType.DateTime) return BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(reader.ReadDateTime());

        // Extended JSON object: { "$date": "2000-01-01T00:00:00Z" } or { "$date": { "$numberLong": "..." } }
        if (bsonType == BsonType.Document)
        {
            reader.ReadStartDocument();
            DateTime? result = null;
            while (reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var name = reader.ReadName();
                if (name == "$date")
                {
                    var inner = reader.GetCurrentBsonType();
                    if (inner == BsonType.String)
                        result = DateTime.Parse(reader.ReadString(), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    else if (inner == BsonType.DateTime)
                        result = BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(reader.ReadDateTime());
                    else if (inner == BsonType.Int64)
                        result = BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(reader.ReadInt64());
                    else if (inner == BsonType.Document)
                    {
                        // { "$numberLong": "..." }
                        reader.ReadStartDocument();
                        reader.ReadBsonType();
                        reader.ReadName(); // "$numberLong"
                        result = BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(long.Parse(reader.ReadString()));
                        reader.ReadEndDocument();
                    }
                    else reader.SkipValue();
                }
                else reader.SkipValue();
            }
            reader.ReadEndDocument();
            return result;
        }

        if (bsonType == BsonType.String)
            return DateTime.Parse(reader.ReadString(), null, System.Globalization.DateTimeStyles.RoundtripKind);

        reader.SkipValue();
        return null;
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime? value)
    {
        if (value == null) context.Writer.WriteNull();
        else context.Writer.WriteDateTime(BsonUtils.ToMillisecondsSinceEpoch(value.Value.ToUniversalTime()));
    }
}
