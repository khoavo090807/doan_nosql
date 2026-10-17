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
        var lhp = await _mongoDbService.LopHocPhans.Find(x => x.Id == maLhp).FirstOrDefaultAsync();
        if (lhp == null) return NotFound("Không tìm thấy lớp học phần.");
        if (CurrentRole == "admin") return null;
        if (CurrentRole != "giaovien") return Forbid();

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

        var enrollmentIds = sv.BangDiem.Select(x => x.MaLHP).ToList();
        if (enrollmentIds.Count != enrollmentIds.Distinct().Count())
            return BadRequest("Hồ sơ không được có đăng ký trùng lớp học phần.");

        sv.TaoTaiKhoan = false;
        sv.MatKhauTaiKhoan = null;
        var reservedClasses = new List<string>();
        foreach (var maLhp in enrollmentIds)
        {
            if (!await TangSiSoAsync(maLhp))
            {
                foreach (var reserved in reservedClasses) await GiamSiSoAsync(reserved);
                return Conflict("Lớp học phần đã đủ sĩ số hoặc không tồn tại.");
            }
            reservedClasses.Add(maLhp);
        }
        try
        {
            await _mongoDbService.SinhViens.InsertOneAsync(sv);
        }
        catch
        {
            foreach (var reserved in reservedClasses) await GiamSiSoAsync(reserved);
            throw;
        }

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
                foreach (var reserved in reservedClasses) await GiamSiSoAsync(reserved);
                throw;
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = sv.Id }, sv);
    }

    /// <summary>Admin thêm đăng ký học phần vào bảng điểm sinh viên.</summary>
    [HttpPost("{id}/diem")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> ThemMonHoc(string id, [FromBody] BangDiemMonHoc monMoi)
    {
        if (string.IsNullOrWhiteSpace(monMoi.MaLHP) || string.IsNullOrWhiteSpace(monMoi.MaMon))
            return BadRequest("Thiếu MaLHP hoặc MaMon.");
        if (!DiemThanhPhanHopLe(monMoi.DiemThanhPhan))
            return BadRequest("Điểm thành phần phải nằm trong khoảng từ 0 đến 10.");

        var permissionError = await KiemTraQuyenNhapSuaDiem(monMoi.MaLHP);
        if (permissionError != null) return permissionError;

        var lhp = await _mongoDbService.LopHocPhans.Find(x => x.Id == monMoi.MaLHP).FirstOrDefaultAsync();
        if (lhp == null) return NotFound("Không tìm thấy lớp học phần.");
        if (lhp.MaMon != monMoi.MaMon)
            return BadRequest("Mã môn học không khớp với lớp học phần.");
        var monHoc = await _mongoDbService.MonHocs.Find(x => x.Id == monMoi.MaMon).FirstOrDefaultAsync();
        if (monHoc == null) return NotFound("Không tìm thấy môn học.");

        var exists = await _mongoDbService.SinhViens
            .Find(x => x.Id == id && x.BangDiem.Any(b => b.MaLHP == monMoi.MaLHP))
            .AnyAsync();
        if (exists) return Conflict("Môn học phần này đã có trong bảng điểm.");

        monMoi.DiemTongKet = GradeCalculator.CalculateFinal(
            monMoi.DiemThanhPhan, GradeCalculator.UsesPractice(monHoc));
        monMoi.DiemChu = GradeCalculator.LetterGrade(monMoi.DiemTongKet);
        monMoi.DiemCLO = GradeCalculator.BuildCloResults(monHoc, monMoi.DiemTongKet);

        if (!await TangSiSoAsync(monMoi.MaLHP)) return Conflict("Lớp đã đủ sĩ số hoặc không còn tồn tại.");

        try
        {
            var addFilter = Builders<SinhVien>.Filter.Eq(x => x.Id, id) &
                Builders<SinhVien>.Filter.Not(Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == monMoi.MaLHP));
            var result = await _mongoDbService.SinhViens.UpdateOneAsync(
                addFilter,
                Builders<SinhVien>.Update.Push(x => x.BangDiem, monMoi));
            if (result.MatchedCount == 0)
            {
                await GiamSiSoAsync(monMoi.MaLHP);
                return Conflict("Sinh vien da dang ky lop hoc phan nay hoac khong ton tai.");
            }
        }
        catch
        {
            var committed = await _mongoDbService.SinhViens.Find(x => x.Id == id && x.BangDiem.Any(b => b.MaLHP == monMoi.MaLHP)).AnyAsync();
            if (!committed) await GiamSiSoAsync(monMoi.MaLHP);
            throw;
        }

        return Ok(monMoi);
    }

    /// <summary>Đặt lại điểm của một học phần mà không xóa đăng ký.</summary>
    [HttpDelete("{id}/diem/{maLhp}")]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult> XoaDiem(string id, string maLhp)
    {
        var permissionError = await KiemTraQuyenNhapSuaDiem(maLhp);
        if (permissionError != null) return permissionError;

        var filter = Builders<SinhVien>.Filter.Eq(x => x.Id, id) &
            Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == maLhp);
        var studentExists = await _mongoDbService.SinhViens.Find(x => x.Id == id).AnyAsync();
        if (!studentExists) return NotFound("Không tìm thấy sinh viên.");
        if (!await _mongoDbService.SinhViens.Find(filter).AnyAsync())
            return NotFound("Sinh viên chưa đăng ký lớp học phần này.");

        var update = Builders<SinhVien>.Update
            .Set("BangDiem.$[element].DiemThanhPhan.ChuyenCan", (double?)null)
            .Set("BangDiem.$[element].DiemThanhPhan.GiuaKy", (double?)null)
            .Set("BangDiem.$[element].DiemThanhPhan.ThucHanh", (double?)null)
            .Set("BangDiem.$[element].DiemThanhPhan.CuoiKy", (double?)null)
            .Set("BangDiem.$[element].DiemTongKet", (double?)null)
            .Set("BangDiem.$[element].DiemChu", (string?)null)
            .Set("BangDiem.$[element].DiemCLO", new List<DiemCLO>());
        var options = new UpdateOptions
        {
            ArrayFilters = new List<ArrayFilterDefinition>
            {
                new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("element.MaLHP", maLhp))
            }
        };
        await _mongoDbService.SinhViens.UpdateOneAsync(filter, update, options);
        return Ok("Đã xóa điểm. Sinh viên vẫn được giữ trong lớp học phần.");
    }

    /// <summary>Admin hủy đăng ký của sinh viên khỏi lớp học phần.</summary>
    [HttpDelete("{id}/lophocphan/{maLhp}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> XoaSinhVienKhoiLop(string id, string maLhp)
    {
        if (!await _mongoDbService.LopHocPhans.Find(x => x.Id == maLhp).AnyAsync())
            return NotFound("Không tìm thấy lớp học phần.");
        var filter = Builders<SinhVien>.Filter.Eq(x => x.Id, id) &
            Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == maLhp);
        var enrollment = await _mongoDbService.SinhViens.Find(filter).FirstOrDefaultAsync();
        var enrollmentCount = enrollment != null && enrollment.BangDiem.Any(b => b.MaLHP == maLhp) ? 1 : 0;
        if (enrollmentCount > 0 && !await GiamSiSoAsync(maLhp))
            return Conflict("Cannot update class count; student registration was kept.");
        var result = await _mongoDbService.SinhViens.UpdateOneAsync(
            filter,
            Builders<SinhVien>.Update.PullFilter(x => x.BangDiem, b => b.MaLHP == maLhp));
        if (result.MatchedCount == 0)
        {
            if (enrollmentCount > 0) await KhoiPhucSiSoAsync(maLhp);
            var studentExists = await _mongoDbService.SinhViens.Find(x => x.Id == id).AnyAsync();
            return studentExists
                ? NotFound("Sinh viên chưa đăng ký lớp học phần này.")
                : NotFound("Không tìm thấy sinh viên.");
        }
        return Ok("Đã xóa sinh viên khỏi lớp học phần.");
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

        foreach (var maLhp in sv.BangDiem.Select(x => x.MaLHP).Distinct())
            await DongBoSiSoLopAsync(maLhp);

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
        
        var monHoc = await _mongoDbService.MonHocs.Find(x => x.Id == monHienTai.MaMon).FirstOrDefaultAsync();
        if (monHoc == null) return NotFound("Không tìm thấy môn học để xác định các thành phần điểm.");
        var tongKet = GradeCalculator.CalculateFinal(diemMoi, GradeCalculator.UsesPractice(monHoc));
        var diemChu = GradeCalculator.LetterGrade(tongKet);

        var update = Builders<SinhVien>.Update
            .Set("BangDiem.$[element].DiemThanhPhan.ChuyenCan", diemMoi.ChuyenCan)
            .Set("BangDiem.$[element].DiemThanhPhan.GiuaKy", diemMoi.GiuaKy)
            .Set("BangDiem.$[element].DiemThanhPhan.ThucHanh", diemMoi.ThucHanh)
            .Set("BangDiem.$[element].DiemThanhPhan.CuoiKy", diemMoi.CuoiKy)
            .Set("BangDiem.$[element].DiemTongKet", tongKet)
            .Set("BangDiem.$[element].DiemChu", diemChu);

        update = update.Set("BangDiem.$[element].DiemCLO", GradeCalculator.BuildCloResults(monHoc, tongKet));

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

        var noPracticeCourseIds = await _mongoDbService.MonHocs
            .Find(x => x.ThucHanh <= 0)
            .Project(x => x.Id)
            .ToListAsync();

        var pipeline = new List<BsonDocument>
        {
            new BsonDocument("$match", new BsonDocument("_id", id)),
            new BsonDocument("$addFields", new BsonDocument("BangDiem", new BsonDocument("$filter", new BsonDocument
            {
                { "input", new BsonDocument("$ifNull", new BsonArray { "$BangDiem", new BsonArray() }) },
                { "as", "b" },
                { "cond", TaoDieuKienCoDiem(noPracticeCourseIds, CurrentRole == "giaovien" ? maLhpList : null) }
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
                BsonNull.Value,
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

        if (result["GPA"].IsBsonNull)
        {
            var student = await _mongoDbService.SinhViens.Find(x => x.Id == id).FirstOrDefaultAsync();
            var hasPartialScores = student?.BangDiem.Any(b =>
                (CurrentRole != "giaovien" || maLhpList!.Contains(b.MaLHP)) &&
                GradeCalculator.HasAnyScore(b.DiemThanhPhan) &&
                !GradeCalculator.IsComplete(b.DiemThanhPhan,
                    !noPracticeCourseIds.Contains(b.MaMon))) == true;
            if (hasPartialScores) result["XepLoai"] = "Ch?a ho?n t?t ?i?m";
        }

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
        var courseIds = sv.BangDiem.Select(b => b.MaMon).Distinct().ToList();
        var courses = await _mongoDbService.MonHocs.Find(x => courseIds.Contains(x.Id)).ToListAsync();
        var coursesById = courses.ToDictionary(x => x.Id);
        bool Complete(BangDiemMonHoc b) =>
            b.DiemTongKet.HasValue &&
            GradeCalculator.IsComplete(b.DiemThanhPhan,
                !coursesById.TryGetValue(b.MaMon, out var course) || GradeCalculator.UsesPractice(course));
        var coDiem = sv.BangDiem.Where(Complete).ToList();
        var tongTinChi = coDiem.Sum(b => b.SoTinChi);
        var gpa = tongTinChi == 0 ? (double?)null : coDiem.Sum(b => b.DiemTongKet!.Value * b.SoTinChi) / tongTinChi;
        var hasPartial = sv.BangDiem.Any(b => GradeCalculator.HasAnyScore(b.DiemThanhPhan) && !Complete(b));
        var xepLoai = coDiem.Count == 0 ? (hasPartial ? "Chưa hoàn tất điểm" : "Chưa có điểm") : gpa!.Value switch
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
                        var complete = Complete(mon);
                        BodyCell(table, complete ? mon.DiemTongKet!.Value.ToString("0.0") : GradeCalculator.ScoreStatus(mon.DiemThanhPhan));
                        BodyCell(table, complete ? (mon.DiemChu ?? GradeCalculator.LetterGrade(mon.DiemTongKet)) ?? "—" : GradeCalculator.ScoreStatus(mon.DiemThanhPhan));
                    }

                    if (bangDiem.Count == 0)
                        table.Cell().ColumnSpan(10).Element(cell => cell.Padding(6).AlignCenter().Text("Chưa có môn học nào."));
                });

                column.Item().PaddingTop(12).Row(row =>
                {
                    row.RelativeItem().Text($"GPA (chỉ tính môn đã hoàn tất điểm): {(gpa.HasValue ? gpa.Value.ToString("0.00") : "—")}").Bold();
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

    /// <summary>Sinh viên đăng ký trong thời gian đăng ký đang mở.</summary>
    [HttpPost("dangky/{maLhp}")]
    [Authorize(Roles = "sinhvien,admin")]
    public async Task<ActionResult> DangKyHocPhan(string maLhp, [FromQuery] string? svId)
    {
        var targetSvId = CurrentRole == "sinhvien" ? CurrentRefId : (svId ?? CurrentRefId);
        if (string.IsNullOrWhiteSpace(targetSvId)) return BadRequest("Không xác định được sinh viên.");

        var lhp = await _mongoDbService.LopHocPhans.Find(x => x.Id == maLhp).FirstOrDefaultAsync();
        if (lhp == null) return NotFound("Không tìm thấy lớp học phần.");

        // DaMo là trạng thái đăng ký, không phải trạng thái điểm hay trạng thái học phần.
        if (!lhp.DaMo)
            return BadRequest("Đã đóng thời gian đăng ký lớp học phần này.");

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
            DiemTongKet = null,
            DiemChu = "Chưa có điểm",
            DiemCLO = new List<DiemCLO>()
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
            var committed = await _mongoDbService.SinhViens.Find(x => x.Id == targetSvId && x.BangDiem.Any(b => b.MaLHP == maLhp)).AnyAsync();
            if (!committed) await GiamSiSoAsync(maLhp);
            throw;
        }

        return Ok(new { message = $"Đăng ký thành công môn '{lhp.TenMon}' ({lhp.Id})", entry });
    }

    /// <summary>Sinh viên chỉ tự hủy đăng ký khi thời gian đăng ký vẫn đang mở.</summary>
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
            return BadRequest("Đã đóng thời gian đăng ký; sinh viên không thể tự hủy lớp học phần.");

        var sv = await _mongoDbService.SinhViens.Find(x => x.Id == targetSvId).FirstOrDefaultAsync();
        if (sv == null) return NotFound("Không tìm thấy sinh viên.");

        var item = sv.BangDiem.FirstOrDefault(b => b.MaLHP == maLhp);
        if (item == null) return NotFound("Sinh viên chưa đăng ký lớp học phần này.");
        var soDangKy = sv.BangDiem.Any(b => b.MaLHP == maLhp) ? 1 : 0;

        // Không cho phép hủy nếu đã có điểm
        var hasScore = item.DiemThanhPhan?.ChuyenCan.HasValue == true ||
                   item.DiemThanhPhan?.GiuaKy.HasValue == true ||
                   item.DiemThanhPhan?.ThucHanh.HasValue == true ||
                   item.DiemThanhPhan?.CuoiKy.HasValue == true ||
                       item.DiemTongKet.GetValueOrDefault() > 0;

        if (hasScore && CurrentRole == "sinhvien")
            return BadRequest("Không thể hủy lớp học phần đã được giáo viên nhập điểm.");

        var enrollmentFilter = Builders<SinhVien>.Filter.Eq(x => x.Id, targetSvId) &
            Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == maLhp);
        if (!await GiamSiSoAsync(maLhp, soDangKy))
            return Conflict("Cannot update class count; student registration was kept.");
        var result = await _mongoDbService.SinhViens.UpdateOneAsync(
            enrollmentFilter,
            Builders<SinhVien>.Update.PullFilter(x => x.BangDiem, b => b.MaLHP == maLhp)
        );
        if (result.ModifiedCount == 0)
        {
            await KhoiPhucSiSoAsync(maLhp);
            return NotFound("Sinh viên chưa đăng ký lớp học phần này.");
        }

        return Ok(new { message = $"Đã hủy đăng ký lớp '{item.TenMon}' ({maLhp})" });
    }

    private async Task<bool> TangSiSoAsync(string maLhp, int soLuong = 1)
    {
        if (soLuong <= 0) return false;
        var filter = new BsonDocumentFilterDefinition<LopHocPhan>(new BsonDocument
        {
            { "_id", maLhp },
            { "$expr", new BsonDocument("$lte", new BsonArray { new BsonDocument("$add", new BsonArray { "$SiSoHienTai", soLuong }), "$SiSoToiDa" }) }
        });
        var result = await _mongoDbService.LopHocPhans.UpdateOneAsync(
            filter,
            Builders<LopHocPhan>.Update.Inc(x => x.SiSoHienTai, soLuong));
        return result.ModifiedCount == 1;
    }

    private async Task<bool> GiamSiSoAsync(string maLhp, int soLuong = 1)
    {
        if (soLuong <= 0) return false;
        var filter = Builders<LopHocPhan>.Filter.Eq(x => x.Id, maLhp) &
            Builders<LopHocPhan>.Filter.Gte(x => x.SiSoHienTai, soLuong);
        var result = await _mongoDbService.LopHocPhans.UpdateOneAsync(
            filter,
            Builders<LopHocPhan>.Update.Inc(x => x.SiSoHienTai, -soLuong));
        return result.ModifiedCount == 1;
    }

    private async Task KhoiPhucSiSoAsync(string maLhp)
    {
        await _mongoDbService.LopHocPhans.UpdateOneAsync(x => x.Id == maLhp,
            Builders<LopHocPhan>.Update.Inc(x => x.SiSoHienTai, 1));
    }

    private async Task DongBoSiSoLopAsync(string maLhp)
    {
        var counts = await _mongoDbService.GetEnrollmentCountsAsync();
        await _mongoDbService.LopHocPhans.UpdateOneAsync(x => x.Id == maLhp,
            Builders<LopHocPhan>.Update.Set(x => x.SiSoHienTai, counts.GetValueOrDefault(maLhp)));
    }

    private static bool DiemThanhPhanHopLe(DiemThanhPhan? diem) => diem != null &&
        DiemTrongKhoang(diem.ChuyenCan) && DiemTrongKhoang(diem.GiuaKy) &&
        DiemTrongKhoang(diem.ThucHanh) && DiemTrongKhoang(diem.CuoiKy);

    private static bool DiemTrongKhoang(double? diem) => !diem.HasValue || (diem.Value >= 0 && diem.Value <= 10);

    private static BsonDocument TaoDieuKienCoDiem(IReadOnlyCollection<string> noPracticeCourseIds, List<string>? maLhpList = null)
    {
        var complete = GradeCalculator.CompletedCourseCondition(noPracticeCourseIds);
        if (maLhpList == null) return complete;
        return new BsonDocument("$and", new BsonArray
        {
            complete,
            new BsonDocument("$in", new BsonArray { "$$b.MaLHP", new BsonArray(maLhpList) })
        });
    }

}
