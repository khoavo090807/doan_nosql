using MongoDB.Bson.Serialization.Attributes;

namespace doan_cuoiky_nosql.Models;

public class Khoa
{
    [BsonId]
    public string Id { get; set; } = null!; 
    
    public string TenKhoa { get; set; } = null!;
    public int NamThanhLap { get; set; }
    public string TruongKhoa { get; set; } = null!;
    public List<string> BoMon { get; set; } = new List<string>();
}