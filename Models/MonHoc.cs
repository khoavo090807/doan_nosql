using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace doan_cuoiky_nosql.Models;

[BsonIgnoreExtraElements]
public class MonHoc
{
    [BsonId]
    public string Id { get; set; } = null!; // MH001
    public string TenMon { get; set; } = null!;
    public int SoTinChi { get; set; }
    public int LyThuyet { get; set; }
    public int ThucHanh { get; set; }
    public string MaKhoa { get; set; } = null!;
    public List<CLOMonHoc> CLO { get; set; } = new List<CLOMonHoc>();
}

public class CLOMonHoc
{
    public string MaCLO { get; set; } = null!;
    public string MoTa { get; set; } = null!;
    public string MucDo { get; set; } = null!;
}