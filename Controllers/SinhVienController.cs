using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SinhVienController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;

    public SinhVienController(MongoDbService mongoDbService)
    {
        _mongoDbService = mongoDbService;
    }

    private string? CurrentRole => User.FindFirstValue(ClaimTypes.Role);
    private string? CurrentRefId => User.FindFirstValue("RefId");

    /// <summary>
    /// Kiểm tra quyền sửa/thêm/xóa điểm của môn theo lớp học phần:
    /// - Admin: toàn quyền
    /// - Giáo viên: phải được phân công và được admin cho phép nhập/sửa điểm
    /// </summary>
    private async Task<ActionResult?> KiemTraQuyenNhapSuaDiem(string maLhp)
    {
        if (CurrentRole == "admin") return null;
        if (CurrentRole != "giaovien") return Forbid();

        var lhp = await _mongoDbService.LopHocPhans.Find(x => x.Id == maLhp).FirstOrDefaultAsync();
        if (lhp == null) return NotFound("Không tìm thấy lớp học phần.");
        if (lhp.GiangVien?.MaGV != CurrentRefId) return Forbid();
        if (!lhp.ChoPhepGvNhapSuaDiem)
            return StatusCode(403, "Đã hết thời gian nhập hoặc chỉnh sửa điểm.");

        return null;
    }

    [HttpGet]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult<List<SinhVien>>> Get([FromQuery] string? lop, [FromQuery] string? q)
    {
        var matchStage = new BsonDocument("$match", new BsonDocument());
        List<string>? maLhpList = null;

        // RBAC: Giáo viên chỉ xem sinh viên trong các lớp họ dạy
        if (CurrentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(CurrentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            // Lấy danh sách MaLHP mà giáo viên này dạy
            var lopHocPhans = await _mongoDbService.LopHocPhans
                .Find(x => x.GiangVien.MaGV == CurrentRefId)
                .ToListAsync();

            maLhpList = lopHocPhans.Select(l => l.Id).ToList();

            if (!maLhpList.Any())
                return Ok(new List<SinhVien>()); // Giáo viên không dạy lớp nào

            // Chỉ lấy sinh viên có ít nhất 1 môn trong các lớp giáo viên dạy
            matchStage["$match"]["BangDiem.MaLHP"] = new BsonDocument("$in", new BsonArray(maLhpList));
        }

        if (!string.IsNullOrWhiteSpace(lop))
            matchStage["$match"]["LopSinhHoat"] = lop;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var regex = new BsonRegularExpression(q, "i");
            matchStage["$match"]["$or"] = new BsonArray
            {
                new BsonDocument("_id", regex),
                new BsonDocument("HoTen", regex)
            };
        }

        var pipeline = new List<BsonDocument> { matchStage };

        if (CurrentRole == "giaovien" && maLhpList != null)
        {
            pipeline.Add(new BsonDocument("$addFields", new BsonDocument("BangDiem", new BsonDocument("$filter", new BsonDocument
            {
                { "input", "$BangDiem" },
                { "as", "b" },
                { "cond", new BsonDocument("$in", new BsonArray { "$$b.MaLHP", new BsonArray(maLhpList) }) }
            }))));
        }

        pipeline.AddRange(new[]
        {
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "khoa" },
                { "localField", "MaKhoa" },
                { "foreignField", "_id" },
                { "as", "KhoaInfo" }
            }),
            new BsonDocument("$unwind", new BsonDocument
            {
                { "path", "$KhoaInfo" },
                { "preserveNullAndEmptyArrays", true }
            }),
            new BsonDocument("$addFields", new BsonDocument("TenKhoa", "$KhoaInfo.TenKhoa")),
            new BsonDocument("$project", new BsonDocument("KhoaInfo", 0)),
            new BsonDocument("$sort", new BsonDocument("_id", 1))
        });

        var result = await _mongoDbService.SinhViens.Aggregate<SinhVien>(pipeline).ToListAsync();
        return result;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SinhVien>> GetById(string id)
    {
        // RBAC: Sinh viên chỉ xem được điểm/thông tin của chính mình
        if (CurrentRole == "sinhvien" && CurrentRefId != id)
            return Forbid();

        List<string>? maLhpList = null;

        // Giáo viên chỉ xem được sinh viên trong lớp mình dạy
        if (CurrentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(CurrentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            var lopHocPhans = await _mongoDbService.LopHocPhans
                .Find(x => x.GiangVien.MaGV == CurrentRefId)
                .ToListAsync();

            maLhpList = lopHocPhans.Select(l => l.Id).ToList();

            var sv = await _mongoDbService.SinhViens.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (sv == null) return NotFound();

            // Kiểm tra sinh viên có môn nào trong lớp giáo viên dạy không
            if (!sv.BangDiem.Any(b => maLhpList.Contains(b.MaLHP)))
                return Forbid();
        }

        var pipeline = new List<BsonDocument>
        {
            new BsonDocument("$match", new BsonDocument("_id", id))
        };

        if (CurrentRole == "giaovien" && maLhpList != null)
        {
            pipeline.Add(new BsonDocument("$addFields", new BsonDocument("BangDiem", new BsonDocument("$filter", new BsonDocument
            {
                { "input", "$BangDiem" },
                { "as", "b" },
                { "cond", new BsonDocument("$in", new BsonArray { "$$b.MaLHP", new BsonArray(maLhpList) }) }
            }))));
        }

        pipeline.AddRange(new[]
        {
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "khoa" },
                { "localField", "MaKhoa" },
                { "foreignField", "_id" },
                { "as", "KhoaInfo" }
            }),
            new BsonDocument("$unwind", new BsonDocument
            {
                { "path", "$KhoaInfo" },
                { "preserveNullAndEmptyArrays", true }
            }),
            new BsonDocument("$addFields", new BsonDocument("TenKhoa", "$KhoaInfo.TenKhoa")),
            new BsonDocument("$project", new BsonDocument("KhoaInfo", 0))
        });

        var svResult = await _mongoDbService.SinhViens.Aggregate<SinhVien>(pipeline).FirstOrDefaultAsync();
        if (svResult == null) return NotFound();
        return svResult;
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Create(SinhVien sv)
    {
        await _mongoDbService.SinhViens.InsertOneAsync(sv);
        return CreatedAtAction(nameof(GetById), new { id = sv.Id }, sv);
    }

    /// <summary>Thêm môn mới vào mảng BangDiem bằng toán tử $push.</summary>
    [HttpPost("{id}/diem")]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult> ThemMonHoc(string id, [FromBody] BangDiemMonHoc monMoi)
    {
        if (string.IsNullOrWhiteSpace(monMoi.MaLHP) || string.IsNullOrWhiteSpace(monMoi.MaMon))
            return BadRequest("Thiếu MaLHP hoặc MaMon.");

        var permissionError = await KiemTraQuyenNhapSuaDiem(monMoi.MaLHP);
        if (permissionError != null) return permissionError;

        var exists = await _mongoDbService.SinhViens
            .Find(x => x.Id == id && x.BangDiem.Any(b => b.MaLHP == monMoi.MaLHP))
            .AnyAsync();
        if (exists) return Conflict("Môn học phần này đã có trong bảng điểm.");

        TinhDiemTongKet(monMoi);
        if (string.IsNullOrWhiteSpace(monMoi.DiemChu)) monMoi.DiemChu = "F";

        var result = await _mongoDbService.SinhViens.UpdateOneAsync(
            Builders<SinhVien>.Filter.Eq(x => x.Id, id),
            Builders<SinhVien>.Update.Push(x => x.BangDiem, monMoi));

        return result.MatchedCount == 0 ? NotFound() : Ok(monMoi);
    }

    /// <summary>Xóa 1 môn học khỏi mảng BangDiem bằng toán tử $pull.</summary>
    [HttpDelete("{id}/diem/{maLhp}")]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult> XoaMonHoc(string id, string maLhp)
    {
        var permissionError = await KiemTraQuyenNhapSuaDiem(maLhp);
        if (permissionError != null) return permissionError;

        var filter = Builders<SinhVien>.Filter.Eq(x => x.Id, id);
        var update = Builders<SinhVien>.Update.PullFilter(x => x.BangDiem, b => b.MaLHP == maLhp);

        var result = await _mongoDbService.SinhViens.UpdateOneAsync(filter, update);
        if (result.MatchedCount == 0) return NotFound("Không tìm thấy sinh viên");
        if (result.ModifiedCount == 0) return NotFound("Môn học không tồn tại trong bảng điểm");

        return Ok("Đã xóa môn khỏi bảng điểm.");
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Update(string id, SinhVien svIn)
    {
        svIn.Id = id;
        var r = await _mongoDbService.SinhViens.ReplaceOneAsync(x => x.Id == id, svIn);
        return r.MatchedCount > 0 ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(string id)
    {
        var r = await _mongoDbService.SinhViens.DeleteOneAsync(x => x.Id == id);
        return r.DeletedCount > 0 ? NoContent() : NotFound();
    }

    /// <summary>Cập nhật điểm thành phần của sinh viên</summary>
    [HttpPut("{id}/diem/{maLhp}")]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult> CapNhatDiemThanhPhan(string id, string maLhp, [FromBody] DiemThanhPhan diemMoi)
    {
        var permissionError = await KiemTraQuyenNhapSuaDiem(maLhp);
        if (permissionError != null) return permissionError;

        var filter = Builders<SinhVien>.Filter.Eq(x => x.Id, id);
        
        var tongKet = TinhDiemTongKetValue(diemMoi);
        var diemChu = QuyDoiDiemChu(tongKet);

        var update = Builders<SinhVien>.Update
            .Set("BangDiem.$[element].DiemThanhPhan.ChuyenCan", diemMoi.ChuyenCan)
            .Set("BangDiem.$[element].DiemThanhPhan.GiuaKy", diemMoi.GiuaKy)
            .Set("BangDiem.$[element].DiemThanhPhan.ThucHanh", diemMoi.ThucHanh)
            .Set("BangDiem.$[element].DiemThanhPhan.CuoiKy", diemMoi.CuoiKy)
            .Set("BangDiem.$[element].DiemTongKet", tongKet)
            .Set("BangDiem.$[element].DiemChu", diemChu);

        var arrayFilters = new List<ArrayFilterDefinition>
        {
            new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("element.MaLHP", maLhp))
        };

        var updateOptions = new UpdateOptions { ArrayFilters = arrayFilters };

        var result = await _mongoDbService.SinhViens.UpdateOneAsync(filter, update, updateOptions);
        if (result.MatchedCount == 0) return NotFound("Không tìm thấy sinh viên");
        if (result.ModifiedCount == 0) return BadRequest("Không có thay đổi hoặc môn học không tồn tại");

        return Ok("Cập nhật điểm thành công");
    }

    // YÊU CẦU: Aggregation Pipeline tính GPA và Xếp loại
    [HttpGet("{id}/hocluc")]
    public async Task<ActionResult> GetHocLuc(string id)
    {
        // RBAC: Sinh viên chỉ xem được điểm/thông tin của chính mình
        if (CurrentRole == "sinhvien" && CurrentRefId != id)
            return Forbid();

        List<string>? maLhpList = null;

        // Giáo viên chỉ xem được GPA của sinh viên trong lớp mình dạy
        if (CurrentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(CurrentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            var lopHocPhans = await _mongoDbService.LopHocPhans
                .Find(x => x.GiangVien.MaGV == CurrentRefId)
                .ToListAsync();

            maLhpList = lopHocPhans.Select(l => l.Id).ToList();

            var sv = await _mongoDbService.SinhViens.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (sv == null) return NotFound();

            // Kiểm tra sinh viên có môn nào trong lớp giáo viên dạy không
            if (!sv.BangDiem.Any(b => maLhpList.Contains(b.MaLHP)))
                return Forbid("Bạn không có quyền xem điểm của sinh viên này.");
        }

        var pipeline = new List<BsonDocument>
        {
            new BsonDocument("$match", new BsonDocument("_id", id)),
            new BsonDocument("$unwind", "$BangDiem")
        };

        if (CurrentRole == "giaovien" && maLhpList != null)
        {
            pipeline.Add(new BsonDocument("$match", new BsonDocument("BangDiem.MaLHP", new BsonDocument("$in", new BsonArray(maLhpList)))));
        }

        pipeline.AddRange(new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$_id" },
                { "HoTen", new BsonDocument("$first", "$HoTen") },
                { "TongDiemHeSo", new BsonDocument("$sum", new BsonDocument("$multiply", new BsonArray { "$BangDiem.DiemTongKet", "$BangDiem.SoTinChi" })) },
                { "TongTinChi", new BsonDocument("$sum", "$BangDiem.SoTinChi") }
            }),
            new BsonDocument("$project", new BsonDocument
            {
                { "HoTen", 1 },
                { "GPA", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$TongTinChi", 0 }),
                        0,
                        new BsonDocument("$divide", new BsonArray { "$TongDiemHeSo", "$TongTinChi" })
                    })
                }
            }),
            new BsonDocument("$project", new BsonDocument
            {
                { "HoTen", 1 },
                { "GPA", 1 },
                { "XepLoai", new BsonDocument("$switch", new BsonDocument("branches", new BsonArray
                    {
                        new BsonDocument { { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 9.0 }) }, { "then", "Xuất sắc" } },
                        new BsonDocument { { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 8.0 }) }, { "then", "Giỏi" } },
                        new BsonDocument { { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 7.0 }) }, { "then", "Khá" } },
                        new BsonDocument { { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 5.0 }) }, { "then", "Trung bình" } }
                    })
                    .Add("default", "Yếu"))
                }
            })
        });

        var result = await _mongoDbService.SinhViens.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();

        if (result == null) return NotFound("Không thể tính điểm hoặc không có dữ liệu bảng điểm");

        result["_id"] = result["_id"].ToString();
        var resDict = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Dictionary<string, object>>(result);
        return Ok(resDict);
    }

    /// <summary>Sinh viên đăng ký lớp học phần</summary>
    [HttpPost("dangky/{maLhp}")]
    [Authorize(Roles = "sinhvien,admin")]
    public async Task<ActionResult> DangKyHocPhan(string maLhp, [FromQuery] string? svId)
    {
        var targetSvId = CurrentRole == "sinhvien" ? CurrentRefId : (svId ?? CurrentRefId);
        if (string.IsNullOrWhiteSpace(targetSvId)) return BadRequest("Không xác định được sinh viên.");

        var lhp = await _mongoDbService.LopHocPhans.Find(x => x.Id == maLhp).FirstOrDefaultAsync();
        if (lhp == null) return NotFound("Không tìm thấy lớp học phần.");

        // Kiểm tra lớp học phần có đang mở để sinh viên đăng ký không
        if (!lhp.DaMo)
            return BadRequest("Lớp học phần này hiện đang đóng, không cho phép đăng ký.");

        var sv = await _mongoDbService.SinhViens.Find(x => x.Id == targetSvId).FirstOrDefaultAsync();
        if (sv == null) return NotFound("Không tìm thấy sinh viên.");

        // RBAC: Sinh viên chỉ được đăng ký cho chính mình
        if (CurrentRole == "sinhvien" && CurrentRefId != targetSvId)
            return Forbid();

        // Kiểm tra đã đăng ký LHP này chưa
        if (sv.BangDiem.Any(b => b.MaLHP == maLhp))
            return BadRequest("Bạn đã đăng ký lớp học phần này rồi.");

        // Kiểm tra đã có môn này chưa
        if (sv.BangDiem.Any(b => b.MaMon == lhp.MaMon))
            return BadRequest($"Bạn đã đăng ký môn {lhp.TenMon} ({lhp.MaMon}) trong một lớp học phần khác.");

        // Lấy thông tin môn học để kiểm tra khoa
        var monHoc = await _mongoDbService.MonHocs.Find(x => x.Id == lhp.MaMon).FirstOrDefaultAsync();
        if (monHoc == null) return NotFound("Không tìm thấy thông tin môn học.");

        // KIỂM TRA KHOA: Sinh viên chỉ được đăng ký môn của khoa mình
        if (CurrentRole == "sinhvien" && monHoc.MaKhoa != sv.MaKhoa)
            return BadRequest($"Bạn không thể đăng ký môn học này. Môn '{monHoc.TenMon}' thuộc khoa '{monHoc.MaKhoa}', trong khi bạn thuộc khoa '{sv.MaKhoa}'.");

        // Kiểm tra sĩ số tối đa
        var currentEnrolled = await _mongoDbService.SinhViens
            .CountDocumentsAsync(x => x.BangDiem.Any(b => b.MaLHP == maLhp));
        if (lhp.SiSoToiDa > 0 && currentEnrolled >= lhp.SiSoToiDa)
            return BadRequest($"Lớp học phần đã đủ sĩ số tối đa ({lhp.SiSoToiDa}/{lhp.SiSoToiDa}).");

        var entry = new BangDiemMonHoc
        {
            MaLHP = lhp.Id,
            MaMon = lhp.MaMon,
            TenMon = lhp.TenMon,
            SoTinChi = lhp.SoTinChi > 0 ? lhp.SoTinChi : (monHoc?.SoTinChi ?? 3),
            GiangVien = lhp.GiangVien ?? new GiangVienPhuTrach(),
            Hocky = lhp.Hocky,
            NamHoc = lhp.NamHoc,
            DiemThanhPhan = new DiemThanhPhan(),
            DiemTongKet = 0,
            DiemChu = "Chưa có",
            DiemCLO = (monHoc?.CLO ?? new List<CLOMonHoc>()).Select(c => new DiemCLO
            {
                MaCLO = c.MaCLO,
                DiemDat = 0,
                KetQua = "Chưa đánh giá"
            }).ToList()
        };

        await _mongoDbService.SinhViens.UpdateOneAsync(
            Builders<SinhVien>.Filter.Eq(x => x.Id, targetSvId),
            Builders<SinhVien>.Update.Push(x => x.BangDiem, entry)
        );

        return Ok(new { message = $"Đăng ký thành công môn '{lhp.TenMon}' ({lhp.Id})", entry });
    }

    /// <summary>Sinh viên hủy đăng ký lớp học phần nếu chưa có điểm</summary>
    [HttpDelete("huydangky/{maLhp}")]
    [Authorize(Roles = "sinhvien,admin")]
    public async Task<ActionResult> HuyDangKyHocPhan(string maLhp, [FromQuery] string? svId)
    {
        var targetSvId = CurrentRole == "sinhvien" ? CurrentRefId : (svId ?? CurrentRefId);
        if (string.IsNullOrWhiteSpace(targetSvId)) return BadRequest("Không xác định được sinh viên.");

        // RBAC: Sinh viên chỉ được hủy đăng ký của chính mình
        if (CurrentRole == "sinhvien" && CurrentRefId != targetSvId)
            return Forbid();

        var lhp = await _mongoDbService.LopHocPhans.Find(x => x.Id == maLhp).FirstOrDefaultAsync();
        if (lhp == null) return NotFound("Không tìm thấy lớp học phần.");
        if (CurrentRole == "sinhvien" && !lhp.DaMo)
            return BadRequest("Lớp học phần đã đóng, không thể hủy đăng ký.");

        var sv = await _mongoDbService.SinhViens.Find(x => x.Id == targetSvId).FirstOrDefaultAsync();
        if (sv == null) return NotFound("Không tìm thấy sinh viên.");

        var item = sv.BangDiem.FirstOrDefault(b => b.MaLHP == maLhp);
        if (item == null) return NotFound("Sinh viên chưa đăng ký lớp học phần này.");

        // Không cho phép hủy nếu đã có điểm
        var hasScore = item.DiemThanhPhan?.ChuyenCan.HasValue == true ||
                   item.DiemThanhPhan?.GiuaKy.HasValue == true ||
                   item.DiemThanhPhan?.ThucHanh.HasValue == true ||
                   item.DiemThanhPhan?.CuoiKy.HasValue == true ||
                       item.DiemTongKet > 0;

        if (hasScore && CurrentRole == "sinhvien")
            return BadRequest("Không thể hủy lớp học phần đã được giáo viên nhập điểm.");

        await _mongoDbService.SinhViens.UpdateOneAsync(
            Builders<SinhVien>.Filter.Eq(x => x.Id, targetSvId),
            Builders<SinhVien>.Update.PullFilter(x => x.BangDiem, b => b.MaLHP == maLhp)
        );

        return Ok(new { message = $"Đã hủy đăng ký lớp '{item.TenMon}' ({maLhp})" });
    }

    private static void TinhDiemTongKet(BangDiemMonHoc mon)
    {
        mon.DiemTongKet = TinhDiemTongKetValue(mon.DiemThanhPhan);
        mon.DiemChu = QuyDoiDiemChu(mon.DiemTongKet);
        if (string.IsNullOrWhiteSpace(mon.DiemChu))
            mon.DiemChu = "F"; // Default to lowest score
    }

    private static double TinhDiemTongKetValue(DiemThanhPhan d)
    {
        var cc = d.ChuyenCan ?? 0;
        var gk = d.GiuaKy ?? 0;
        var th = d.ThucHanh ?? 0;
        var ck = d.CuoiKy ?? 0;
        return Math.Round(cc * 0.1 + gk * 0.2 + th * 0.2 + ck * 0.5, 1);
    }

    private static string QuyDoiDiemChu(double diem) => diem switch
    {
        >= 9.0 => "A+",
        >= 8.5 => "A",
        >= 8.0 => "B+",
        >= 7.0 => "B",
        >= 6.5 => "C+",
        >= 5.5 => "C",
        >= 5.0 => "D+",
        >= 4.0 => "D",
        _ => "F"
    };
}