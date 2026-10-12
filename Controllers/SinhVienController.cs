using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Claims;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SinhVienController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly IMemoryCache _cache;

    public SinhVienController(MongoDbService mongoDbService, IMemoryCache cache)
    {
        _mongoDbService = mongoDbService;
        _cache = cache;
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
        var taoTaiKhoan = sv.TaoTaiKhoan;
        var matKhau = sv.MatKhauTaiKhoan;
        if (taoTaiKhoan)
        {
            if (string.IsNullOrWhiteSpace(matKhau) || matKhau.Trim().Length < 6)
                return BadRequest("Mật khẩu tài khoản sinh viên phải có tối thiểu 6 ký tự.");

            if (await _mongoDbService.TaiKhoans.Find(x => x.Id == sv.Id).AnyAsync())
                return Conflict($"Tài khoản '{sv.Id}' đã tồn tại.");
        }

        sv.TaoTaiKhoan = false;
        sv.MatKhauTaiKhoan = null;
        await _mongoDbService.SinhViens.InsertOneAsync(sv);

        foreach (var enrollment in sv.BangDiem.GroupBy(x => x.MaLHP))
            await TangSiSoAsync(enrollment.Key, enrollment.Count());

        if (taoTaiKhoan)
        {
            var account = new TaiKhoan
            {
                Id = sv.Id,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(matKhau),
                Role = "sinhvien",
                RefId = sv.Id,
                HoTen = sv.HoTen,
                IsActive = true
            };

            try
            {
                await _mongoDbService.TaiKhoans.InsertOneAsync(account);
            }
            catch
            {
                await _mongoDbService.SinhViens.DeleteOneAsync(x => x.Id == sv.Id);
                foreach (var enrollment in sv.BangDiem.GroupBy(x => x.MaLHP))
                    await GiamSiSoAsync(enrollment.Key, enrollment.Count());
                throw;
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = sv.Id }, sv);
    }

    /// <summary>Thêm môn mới vào mảng BangDiem bằng toán tử $push.</summary>
    [HttpPost("{id}/diem")]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult> ThemMonHoc(string id, [FromBody] BangDiemMonHoc monMoi)
    {
        if (string.IsNullOrWhiteSpace(monMoi.MaLHP) || string.IsNullOrWhiteSpace(monMoi.MaMon))
            return BadRequest("Thiếu MaLHP hoặc MaMon.");
        if (!DiemThanhPhanHopLe(monMoi.DiemThanhPhan))
            return BadRequest("Điểm thành phần phải nằm trong khoảng từ 0 đến 10.");

        var permissionError = await KiemTraQuyenNhapSuaDiem(monMoi.MaLHP);
        if (permissionError != null) return permissionError;

        var exists = await _mongoDbService.SinhViens
            .Find(x => x.Id == id && x.BangDiem.Any(b => b.MaLHP == monMoi.MaLHP))
            .AnyAsync();
        if (exists) return Conflict("Môn học phần này đã có trong bảng điểm.");

        TinhDiemTongKet(monMoi);
        if (string.IsNullOrWhiteSpace(monMoi.DiemChu)) monMoi.DiemChu = "F";
        if (CoDiem(monMoi.DiemThanhPhan))
        {
            if (monMoi.DiemCLO == null || monMoi.DiemCLO.Count == 0)
            {
                var monHoc = await _mongoDbService.MonHocs.Find(x => x.Id == monMoi.MaMon).FirstOrDefaultAsync();
                monMoi.DiemCLO = (monHoc?.CLO ?? new List<CLOMonHoc>()).Select(clo => new DiemCLO
                {
                    MaCLO = clo.MaCLO,
                    DiemDat = monMoi.DiemTongKet,
                    KetQua = monMoi.DiemTongKet >= 5 ? "Đạt" : "Không đạt"
                }).ToList();
            }
            else
            {
                foreach (var clo in monMoi.DiemCLO)
                {
                    clo.DiemDat = monMoi.DiemTongKet;
                    clo.KetQua = monMoi.DiemTongKet >= 5 ? "Đạt" : "Không đạt";
                }
            }
        }

        var addFilter = Builders<SinhVien>.Filter.Eq(x => x.Id, id) &
            Builders<SinhVien>.Filter.Not(Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == monMoi.MaLHP));
        var result = await _mongoDbService.SinhViens.UpdateOneAsync(
            addFilter,
            Builders<SinhVien>.Update.Push(x => x.BangDiem, monMoi));

        if (result.MatchedCount == 0)
            return Conflict("Môn học phần này đã có trong bảng điểm hoặc không tìm thấy sinh viên.");

        await TangSiSoAsync(monMoi.MaLHP);
        return Ok(monMoi);
    }

    /// <summary>Xóa 1 môn học khỏi mảng BangDiem bằng toán tử $pull.</summary>
    [HttpDelete("{id}/diem/{maLhp}")]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult> XoaMonHoc(string id, string maLhp)
    {
        var permissionError = await KiemTraQuyenNhapSuaDiem(maLhp);
        if (permissionError != null) return permissionError;

        var sv = await _mongoDbService.SinhViens.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (sv == null) return NotFound("Không tìm thấy sinh viên.");
        var soDangKy = sv.BangDiem.Count(b => b.MaLHP == maLhp);
        if (soDangKy == 0) return NotFound("Môn học không tồn tại trong bảng điểm.");

        var filter = Builders<SinhVien>.Filter.Eq(x => x.Id, id);
        var update = Builders<SinhVien>.Update.PullFilter(x => x.BangDiem, b => b.MaLHP == maLhp);

        var result = await _mongoDbService.SinhViens.UpdateOneAsync(filter, update);
        if (result.MatchedCount == 0) return NotFound("Không tìm thấy sinh viên");
        if (result.ModifiedCount == 0) return NotFound("Môn học không tồn tại trong bảng điểm");

        await GiamSiSoAsync(maLhp, soDangKy);

        return Ok("Đã xóa môn khỏi bảng điểm.");
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Update(string id, SinhVien svIn)
    {
        var update = Builders<SinhVien>.Update
            .Set(x => x.HoTen, svIn.HoTen)
            .Set(x => x.NgaySinh, svIn.NgaySinh)
            .Set(x => x.GioiTinh, svIn.GioiTinh)
            .Set(x => x.Email, svIn.Email)
            .Set(x => x.DienThoai, svIn.DienThoai)
            .Set(x => x.KhoaHoc, svIn.KhoaHoc)
            .Set(x => x.LopSinhHoat, svIn.LopSinhHoat)
            .Set(x => x.MaKhoa, svIn.MaKhoa)
            .Set(x => x.TrangThai, svIn.TrangThai);
        var r = await _mongoDbService.SinhViens.UpdateOneAsync(x => x.Id == id, update);
        return r.MatchedCount > 0 ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(string id)
    {
        var sv = await _mongoDbService.SinhViens.FindOneAndDeleteAsync(x => x.Id == id);
        if (sv == null) return NotFound();

        foreach (var enrollment in sv.BangDiem.GroupBy(x => x.MaLHP))
            await GiamSiSoAsync(enrollment.Key, enrollment.Count());

        var accounts = await _mongoDbService.TaiKhoans
            .Find(x => x.RefId == id && x.Role == "sinhvien")
            .Project(x => x.Id)
            .ToListAsync();
        await _mongoDbService.TaiKhoans.DeleteManyAsync(x => x.RefId == id && x.Role == "sinhvien");
        foreach (var username in accounts)
            _cache.Remove($"user_active_{username}");

        return NoContent();
    }

    /// <summary>Cập nhật điểm thành phần của sinh viên</summary>
    [HttpPut("{id}/diem/{maLhp}")]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult> CapNhatDiemThanhPhan(string id, string maLhp, [FromBody] DiemThanhPhan diemMoi)
    {
        if (!DiemThanhPhanHopLe(diemMoi))
            return BadRequest("Điểm thành phần phải nằm trong khoảng từ 0 đến 10.");

        var permissionError = await KiemTraQuyenNhapSuaDiem(maLhp);
        if (permissionError != null) return permissionError;

        var filter = Builders<SinhVien>.Filter.Eq(x => x.Id, id);
        var svHienTai = await _mongoDbService.SinhViens.Find(filter).FirstOrDefaultAsync();
        if (svHienTai == null) return NotFound("Không tìm thấy sinh viên.");
        var monHienTai = svHienTai.BangDiem.FirstOrDefault(b => b.MaLHP == maLhp);
        if (monHienTai == null) return NotFound("Môn học phần không tồn tại trong bảng điểm.");
        
        var tongKet = TinhDiemTongKetValue(diemMoi);
        var diemChu = QuyDoiDiemChu(tongKet);

        var update = Builders<SinhVien>.Update
            .Set("BangDiem.$[element].DiemThanhPhan.ChuyenCan", diemMoi.ChuyenCan)
            .Set("BangDiem.$[element].DiemThanhPhan.GiuaKy", diemMoi.GiuaKy)
            .Set("BangDiem.$[element].DiemThanhPhan.ThucHanh", diemMoi.ThucHanh)
            .Set("BangDiem.$[element].DiemThanhPhan.CuoiKy", diemMoi.CuoiKy)
            .Set("BangDiem.$[element].DiemTongKet", tongKet)
            .Set("BangDiem.$[element].DiemChu", diemChu);

        if (CoDiem(diemMoi))
        {
            var ketQuaClo = tongKet >= 5 ? "Đạt" : "Không đạt";
            var diemClo = monHienTai.DiemCLO ?? new List<DiemCLO>();
            if (diemClo.Count == 0)
            {
                var monHoc = await _mongoDbService.MonHocs.Find(x => x.Id == monHienTai.MaMon).FirstOrDefaultAsync();
                diemClo = (monHoc?.CLO ?? new List<CLOMonHoc>()).Select(clo => new DiemCLO
                {
                    MaCLO = clo.MaCLO,
                    DiemDat = tongKet,
                    KetQua = ketQuaClo
                }).ToList();
            }
            else
            {
                foreach (var clo in diemClo)
                {
                    clo.DiemDat = tongKet;
                    clo.KetQua = ketQuaClo;
                }
            }

            update = update.Set("BangDiem.$[element].DiemCLO", diemClo);
        }

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
            new BsonDocument("$addFields", new BsonDocument("BangDiem", new BsonDocument("$filter", new BsonDocument
            {
                { "input", new BsonDocument("$ifNull", new BsonArray { "$BangDiem", new BsonArray() }) },
                { "as", "b" },
                { "cond", TaoDieuKienCoDiem(CurrentRole == "giaovien" ? maLhpList : null) }
            }))),
            new BsonDocument("$unwind", new BsonDocument
            {
                { "path", "$BangDiem" },
                { "preserveNullAndEmptyArrays", true }
            })
        };

        pipeline.AddRange(new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$_id" },
                { "HoTen", new BsonDocument("$first", "$HoTen") },
                { "TongDiemHeSo", new BsonDocument("$sum", new BsonDocument("$multiply", new BsonArray { "$BangDiem.DiemTongKet", "$BangDiem.SoTinChi" })) },
                { "TongTinChi", new BsonDocument("$sum", "$BangDiem.SoTinChi") },
                { "SoMonCoDiem", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$ifNull", new BsonArray { "$BangDiem.MaLHP", false }),
                    1,
                    0
                })) }
            }),
            new BsonDocument("$project", new BsonDocument
            {
                { "HoTen", 1 },
                { "TongTinChi", 1 },
                { "SoMonCoDiem", 1 },
                { "GPA", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$or", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$SoMonCoDiem", 0 }),
                            new BsonDocument("$eq", new BsonArray { "$TongTinChi", 0 })
                        }),
                        0,
                        new BsonDocument("$divide", new BsonArray { "$TongDiemHeSo", "$TongTinChi" })
                    })
                }
            }),
            new BsonDocument("$project", new BsonDocument
            {
                { "HoTen", 1 },
                { "GPA", 1 },
                { "XepLoai", new BsonDocument("$cond", new BsonArray
                    {
                    new BsonDocument("$eq", new BsonArray { "$SoMonCoDiem", 0 }),
                    "Chưa có điểm",
                    new BsonDocument("$switch", new BsonDocument("branches", new BsonArray
                    {
                        new BsonDocument { { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 9.0 }) }, { "then", "Xuất sắc" } },
                        new BsonDocument { { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 8.0 }) }, { "then", "Giỏi" } },
                        new BsonDocument { { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 7.0 }) }, { "then", "Khá" } },
                        new BsonDocument { { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 5.0 }) }, { "then", "Trung bình" } }
                    }).Add("default", "Yếu"))
                })
                }
            })
        });

        var result = await _mongoDbService.SinhViens.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();

        if (result == null) return NotFound("Không thể tính điểm hoặc không có dữ liệu bảng điểm");

        result["_id"] = result["_id"].ToString();
        var resDict = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Dictionary<string, object>>(result);
        return Ok(resDict);
    }

    [HttpGet("{id}/bangdiem/pdf")]
    [Authorize(Roles = "admin,sinhvien")]
    public async Task<IActionResult> TaiBangDiemPdf(string id)
    {
        if (CurrentRole == "sinhvien" && CurrentRefId != id)
            return Forbid();

        var sv = await _mongoDbService.SinhViens.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (sv == null) return NotFound("Không tìm thấy sinh viên.");

        var khoa = await _mongoDbService.Khoas.Find(x => x.Id == sv.MaKhoa).FirstOrDefaultAsync();
        var tenKhoa = khoa?.TenKhoa ?? sv.MaKhoa ?? "";
        var coDiem = sv.BangDiem.Where(b =>
            b.DiemThanhPhan?.ChuyenCan.HasValue == true ||
            b.DiemThanhPhan?.GiuaKy.HasValue == true ||
            b.DiemThanhPhan?.ThucHanh.HasValue == true ||
            b.DiemThanhPhan?.CuoiKy.HasValue == true).ToList();
        var tongTinChi = coDiem.Sum(b => b.SoTinChi);
        var gpa = tongTinChi == 0 ? 0 : coDiem.Sum(b => b.DiemTongKet * b.SoTinChi) / tongTinChi;
        var xepLoai = coDiem.Count == 0 ? "Chưa có điểm" : gpa switch
        {
            >= 9 => "Xuất sắc",
            >= 8 => "Giỏi",
            >= 7 => "Khá",
            >= 5 => "Trung bình",
            _ => "Yếu"
        };
        var bangDiem = sv.BangDiem
            .OrderBy(b => b.NamHoc)
            .ThenBy(b => b.Hocky)
            .ThenBy(b => b.MaLHP)
            .ToList();

        var pdf = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontFamily("Noto Sans").FontSize(9));

            page.Header().Column(column =>
            {
                column.Item().AlignCenter().Text("TRƯỜNG ĐẠI HỌC CÔNG THƯƠNG TP.HỒ CHÍ MINH")
                    .Bold().FontSize(13);
                column.Item().PaddingTop(5).AlignCenter().Text("BẢNG ĐIỂM CÁ NHÂN")
                    .Bold().FontSize(18).FontColor(Colors.Blue.Darken2);
            });

            page.Content().PaddingVertical(14).Column(column =>
            {
                column.Item().PaddingBottom(10).Row(row =>
                {
                    row.RelativeItem().Text($"MSSV: {sv.Id}");
                    row.RelativeItem().Text($"Họ tên: {sv.HoTen}");
                    row.RelativeItem().Text($"Lớp: {sv.LopSinhHoat}");
                    row.RelativeItem().Text($"Khoa: {tenKhoa}");
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.1f);  // MaLHP
                        columns.RelativeColumn(.9f);   // Hoc ky/Nam hoc
                        columns.RelativeColumn(2.5f);  // TenMon
                        columns.RelativeColumn(.55f);  // SoTinChi
                        columns.RelativeColumn(.55f);  // CC
                        columns.RelativeColumn(.55f);  // GK
                        columns.RelativeColumn(.55f);  // TH
                        columns.RelativeColumn(.55f);  // CK
                        columns.RelativeColumn(.7f);   // Tong ket
                        columns.RelativeColumn(.65f);  // Diem chu
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header, "Mã LHP");
                        HeaderCell(header, "Học kỳ / Năm học");
                        HeaderCell(header, "Tên môn");
                        HeaderCell(header, "TC");
                        HeaderCell(header, "CC");
                        HeaderCell(header, "GK");
                        HeaderCell(header, "TH");
                        HeaderCell(header, "CK");
                        HeaderCell(header, "Tổng kết");
                        HeaderCell(header, "Điểm chữ");
                    });

                    foreach (var mon in bangDiem)
                    {
                        BodyCell(table, mon.MaLHP);
                        BodyCell(table, $"{mon.Hocky} / {mon.NamHoc}");
                        BodyCell(table, mon.TenMon);
                        BodyCell(table, mon.SoTinChi.ToString());
                        BodyCell(table, mon.DiemThanhPhan?.ChuyenCan?.ToString("0.##") ?? "—");
                        BodyCell(table, mon.DiemThanhPhan?.GiuaKy?.ToString("0.##") ?? "—");
                        BodyCell(table, mon.DiemThanhPhan?.ThucHanh?.ToString("0.##") ?? "—");
                        BodyCell(table, mon.DiemThanhPhan?.CuoiKy?.ToString("0.##") ?? "—");
                        BodyCell(table, mon.DiemTongKet.ToString("0.0"));
                        BodyCell(table, mon.DiemChu ?? "Chưa có");
                    }

                    if (bangDiem.Count == 0)
                        table.Cell().ColumnSpan(10).Element(cell => cell.Padding(6).AlignCenter().Text("Chưa có môn học nào."));
                });

                column.Item().PaddingTop(12).Row(row =>
                {
                    row.RelativeItem().Text($"GPA (chỉ tính môn đã có điểm): {gpa:0.00}").Bold();
                    row.RelativeItem().Text($"Xếp loại: {xepLoai}").Bold();
                });
                column.Item().PaddingTop(8).AlignRight().Text($"Ngày xuất: {DateTime.Now:dd/MM/yyyy}");
                column.Item().PaddingTop(24).AlignRight().Column(signature =>
                {
                    signature.Item().AlignCenter().Text("NGƯỜI LẬP BẢNG").Bold();
                    signature.Item().PaddingTop(42).AlignCenter().Text("(Ký và ghi rõ họ tên)");
                });
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Trang ");
                text.CurrentPageNumber();
            });
        })).GeneratePdf();

        return File(pdf, "application/pdf", $"BangDiem_{sv.Id}.pdf");
    }

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter().Text(text).FontColor(Colors.White).Bold();

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text);

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

        var slotFilter = new BsonDocumentFilterDefinition<LopHocPhan>(new BsonDocument
        {
            { "_id", maLhp },
            { "DaMo", true },
            { "$expr", new BsonDocument("$lt", new BsonArray { "$SiSoHienTai", "$SiSoToiDa" }) }
        });
        var slotResult = await _mongoDbService.LopHocPhans.UpdateOneAsync(
            slotFilter,
            Builders<LopHocPhan>.Update.Inc(x => x.SiSoHienTai, 1));
        if (slotResult.MatchedCount == 0)
            return BadRequest("Lớp đã đủ sĩ số.");

        try
        {
            var pushFilter = Builders<SinhVien>.Filter.Eq(x => x.Id, targetSvId) &
                Builders<SinhVien>.Filter.Not(
                    Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == maLhp));
            var pushResult = await _mongoDbService.SinhViens.UpdateOneAsync(
                pushFilter,
                Builders<SinhVien>.Update.Push(x => x.BangDiem, entry));

            if (pushResult.MatchedCount == 0)
            {
                await GiamSiSoAsync(maLhp);
                return BadRequest("Sinh viên đã đăng ký lớp học phần này hoặc không còn tồn tại.");
            }
        }
        catch
        {
            await GiamSiSoAsync(maLhp);
            throw;
        }

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
        var soDangKy = sv.BangDiem.Count(b => b.MaLHP == maLhp);

        // Không cho phép hủy nếu đã có điểm
        var hasScore = item.DiemThanhPhan?.ChuyenCan.HasValue == true ||
                   item.DiemThanhPhan?.GiuaKy.HasValue == true ||
                   item.DiemThanhPhan?.ThucHanh.HasValue == true ||
                   item.DiemThanhPhan?.CuoiKy.HasValue == true ||
                       item.DiemTongKet > 0;

        if (hasScore && CurrentRole == "sinhvien")
            return BadRequest("Không thể hủy lớp học phần đã được giáo viên nhập điểm.");

        var result = await _mongoDbService.SinhViens.UpdateOneAsync(
            Builders<SinhVien>.Filter.Eq(x => x.Id, targetSvId),
            Builders<SinhVien>.Update.PullFilter(x => x.BangDiem, b => b.MaLHP == maLhp)
        );
        if (result.ModifiedCount > 0)
            await GiamSiSoAsync(maLhp, soDangKy);

        return Ok(new { message = $"Đã hủy đăng ký lớp '{item.TenMon}' ({maLhp})" });
    }

    private async Task TangSiSoAsync(string maLhp, int soLuong = 1)
    {
        if (soLuong <= 0) return;
        await _mongoDbService.LopHocPhans.UpdateOneAsync(
            x => x.Id == maLhp,
            Builders<LopHocPhan>.Update.Inc(x => x.SiSoHienTai, soLuong));
    }

    private async Task GiamSiSoAsync(string maLhp, int soLuong = 1)
    {
        if (soLuong <= 0) return;
        var filter = Builders<LopHocPhan>.Filter.Eq(x => x.Id, maLhp) &
            Builders<LopHocPhan>.Filter.Gte(x => x.SiSoHienTai, soLuong);
        await _mongoDbService.LopHocPhans.UpdateOneAsync(
            filter,
            Builders<LopHocPhan>.Update.Inc(x => x.SiSoHienTai, -soLuong));
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

    private static bool DiemThanhPhanHopLe(DiemThanhPhan? diem) => diem != null &&
        DiemTrongKhoang(diem.ChuyenCan) && DiemTrongKhoang(diem.GiuaKy) &&
        DiemTrongKhoang(diem.ThucHanh) && DiemTrongKhoang(diem.CuoiKy);

    private static bool DiemTrongKhoang(double? diem) => !diem.HasValue || (diem.Value >= 0 && diem.Value <= 10);

    private static bool CoDiem(DiemThanhPhan? diem) =>
        diem?.ChuyenCan.HasValue == true || diem?.GiuaKy.HasValue == true ||
        diem?.ThucHanh.HasValue == true || diem?.CuoiKy.HasValue == true;

    private static BsonDocument TaoDieuKienCoDiem(List<string>? maLhpList = null)
    {
        var coDiem = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("$ne", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$$b.DiemThanhPhan.ChuyenCan", BsonNull.Value }), BsonNull.Value }),
            new BsonDocument("$ne", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$$b.DiemThanhPhan.GiuaKy", BsonNull.Value }), BsonNull.Value }),
            new BsonDocument("$ne", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$$b.DiemThanhPhan.ThucHanh", BsonNull.Value }), BsonNull.Value }),
            new BsonDocument("$ne", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$$b.DiemThanhPhan.CuoiKy", BsonNull.Value }), BsonNull.Value })
        });

        if (maLhpList == null) return coDiem;
        return new BsonDocument("$and", new BsonArray
        {
            coDiem,
            new BsonDocument("$in", new BsonArray { "$$b.MaLHP", new BsonArray(maLhpList) })
        });
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
