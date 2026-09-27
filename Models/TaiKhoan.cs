using MongoDB.Bson.Serialization.Attributes;

namespace doan_cuoiky_nosql.Models;

[BsonIgnoreExtraElements]
public class TaiKhoan
{
    [BsonId]
    public string Id { get; set; } = null!;          // username
    public string PasswordHash { get; set; } = null!;
    /// <summary>"admin" | "giaovien" | "sinhvien"</summary>
    public string Role { get; set; } = null!;
    /// <summary>MaGV nếu giaovien, MSSV nếu sinhvien, null nếu admin</summary>
    public string? RefId { get; set; }
    public string HoTen { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
