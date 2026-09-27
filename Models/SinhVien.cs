using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace doan_cuoiky_nosql.Models;

[BsonIgnoreExtraElements]
public class SinhVien
{
    [BsonId]
    public string Id { get; set; } = null!;
    public string HoTen { get; set; } = null!;

    [BsonSerializer(typeof(FlexibleDateTimeSerializer))]
    public DateTime? NgaySinh { get; set; }
    public string GioiTinh { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DienThoai { get; set; } = null!;
    public string KhoaHoc { get; set; } = null!;
    public string LopSinhHoat { get; set; } = null!;
    public string MaKhoa { get; set; } = null!;
    public string TrangThai { get; set; } = null!;

    [BsonIgnoreIfNull]
    public string? TenKhoa { get; set; }

    public List<BangDiemMonHoc> BangDiem { get; set; } = new List<BangDiemMonHoc>();
}

[BsonIgnoreExtraElements]
public class BangDiemMonHoc
{
    public string MaLHP { get; set; } = null!;
    public string MaMon { get; set; } = null!;
    public string TenMon { get; set; } = null!;
    
    // Theo yêu cầu mục 2, phải có SoTinChi và GiangVien nhưng dump thiếu, ta nạp thêm!
    public int SoTinChi { get; set; }
    public GiangVienPhuTrach GiangVien { get; set; } = new GiangVienPhuTrach();
    
    public string Hocky { get; set; } = null!;
    public string NamHoc { get; set; } = null!;
    
    public DiemThanhPhan DiemThanhPhan { get; set; } = new DiemThanhPhan();
    
    public double DiemTongKet { get; set; }
    public string? DiemChu { get; set; } = null;
    
    public List<DiemCLO> DiemCLO { get; set; } = new List<DiemCLO>();
}

public class DiemThanhPhan
{
    public double? ChuyenCan { get; set; }
    public double? GiuaKy { get; set; }
    public double? ThucHanh { get; set; }
    public double? CuoiKy { get; set; }
}

public class DiemCLO
{
    public string MaCLO { get; set; } = null!;
    public double? DiemDat { get; set; }
    public string KetQua { get; set; } = null!;
}