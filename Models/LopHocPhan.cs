using MongoDB.Bson.Serialization.Attributes;

namespace doan_cuoiky_nosql.Models;

[BsonIgnoreExtraElements]
public class LopHocPhan
{
    [BsonId]
    public string Id { get; set; } = null!; // LHP_MH001_HK1_2026
    public string MaMon { get; set; } = null!;
    public string TenMon { get; set; } = null!;
    public int SoTinChi { get; set; }
    public string Hocky { get; set; } = null!;
    public string NamHoc { get; set; } = null!;
    public GiangVienPhuTrach GiangVien { get; set; } = null!;
    public string PhongHoc { get; set; } = null!;
    public string LichHoc { get; set; } = null!;
    public int SiSoToiDa { get; set; }
    /// <summary>
    /// Trạng thái mở/đóng lớp học phần cho sinh viên đăng ký.
    /// true = mở (sinh viên có thể đăng ký), false = đóng (không cho đăng ký).
    /// Default true để tương thích với dữ liệu cũ.
    /// </summary>
    public bool DaMo { get; set; } = true;

    public bool ChoPhepGvNhapSuaDiem { get; set; }
}

[BsonIgnoreExtraElements]
public class GiangVienPhuTrach
{
    public string MaGV { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    // Một số bản ghi cũ trong MongoDB dùng tên trường TenGV.
    // Giữ cả hai tên để tương thích khi đọc dữ liệu hiện có.
    public string? TenGV { get; set; }
}

/// <summary>Request body để thay đổi trạng thái mở/đóng lớp học phần.</summary>
public class TrangThaiLhp
{
    public bool DaMo { get; set; }
}
