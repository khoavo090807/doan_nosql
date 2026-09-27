using MongoDB.Bson.Serialization.Attributes;

namespace doan_cuoiky_nosql.Models;

public class GiangVien
{
    [BsonId]
    public string Id { get; set; } = null!;
    
    public string HoTen { get; set; } = null!;
    public string HocVi { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DienThoai { get; set; } = null!;
    public string MaKhoa { get; set; } = null!;
    public string BoMon { get; set; } = null!;
}