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
public class LopHocPhanController : ControllerBase
{
    private readonly MongoDbService _db;

    public LopHocPhanController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet("siso/kiemtra")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> KiemTraSiSo()
    {
        var counts = await _db.GetEnrollmentCountsAsync();
        var classes = await _db.LopHocPhans.Find(_ => true).ToListAsync();
        return Ok(classes.Select(l => new
        {
            l.Id, l.MaMon, l.TenMon,
            SiSoDangLuu = l.SiSoHienTai,
            SiSoThucTe = counts.GetValueOrDefault(l.Id),
            Lech = l.SiSoHienTai != counts.GetValueOrDefault(l.Id)
        }).Where(x => x.Lech).ToList());
    }

    [HttpPost("siso/dongbo")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> DongBoSiSo()
    {
        var counts = await _db.GetEnrollmentCountsAsync();
        var classes = await _db.LopHocPhans.Find(_ => true).ToListAsync();
        var changed = new List<object>();
        foreach (var lhp in classes)
        {
            var actual = counts.GetValueOrDefault(lhp.Id);
            if (lhp.SiSoHienTai == actual) continue;
            var result = await _db.LopHocPhans.UpdateOneAsync(
                x => x.Id == lhp.Id && x.SiSoHienTai == lhp.SiSoHienTai,
                Builders<LopHocPhan>.Update.Set(x => x.SiSoHienTai, actual));
            if (result.ModifiedCount > 0)
                changed.Add(new { lhp.Id, SiSoCu = lhp.SiSoHienTai, SiSoMoi = actual });
        }
        return Ok(new { updated = changed.Count, classes = changed, message = "Đã đồng bộ sĩ số theo số sinh viên có đăng ký." });
    }

    [HttpGet]
    public async Task<ActionResult> Get([FromQuery] string? maGv, [FromQuery] string? maMon)
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        var filter = Builders<LopHocPhan>.Filter.Empty;

        // RBAC: Giáo viên chỉ thấy các lớp mình dạy
        if (currentRole == "giaovien")
        {
            if (!string.IsNullOrWhiteSpace(currentRefId))
            {
                filter &= Builders<LopHocPhan>.Filter.Eq("GiangVien.MaGV", currentRefId);
            }
            else
            {
                return BadRequest("Không xác định được mã giảng viên.");
            }
        }
        // RBAC: Sinh viên chỉ thấy lớp học phần của các môn thuộc khoa mình
        else if (currentRole == "sinhvien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã sinh viên.");

            var sv = await _db.SinhViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (sv == null)
                return NotFound("Không tìm thấy thông tin sinh viên.");

            // Lấy danh sách mã môn học thuộc khoa của sinh viên
            var monHocCuaKhoa = await _db.MonHocs
                .Find(x => x.MaKhoa == sv.MaKhoa)
                .Project(x => x.Id)
                .ToListAsync();

            if (!monHocCuaKhoa.Any())
                return Ok(new List<object>());

            // Chỉ lấy lớp học phần của các môn thuộc khoa sinh viên
            filter &= Builders<LopHocPhan>.Filter.In(x => x.MaMon, monHocCuaKhoa);
        }
        // Admin có thể filter theo maGv nếu cần
        else if (!string.IsNullOrWhiteSpace(maGv))
        {
            filter &= Builders<LopHocPhan>.Filter.Eq("GiangVien.MaGV", maGv.Trim());
        }

        if (!string.IsNullOrWhiteSpace(maMon))
            filter &= Builders<LopHocPhan>.Filter.Eq(x => x.MaMon, maMon.Trim());

        var list = await _db.LopHocPhans.Find(filter).ToListAsync();

        var monHocs = await _db.MonHocs.Find(Builders<MonHoc>.Filter.Empty).ToListAsync();
        var monHocTinChiMap = monHocs.ToDictionary(m => m.Id, m => m.SoTinChi);

        var result = list.Select(l => new
        {
            l.Id,
            l.MaMon,
            l.TenMon,
            SoTinChi = l.SoTinChi > 0 ? l.SoTinChi : (monHocTinChiMap.TryGetValue(l.MaMon, out var tc) ? tc : 3),
            l.Hocky,
            l.NamHoc,
            l.GiangVien,
            l.PhongHoc,
            l.LichHoc,
            l.SiSoToiDa,
            l.DaMo,
            l.ChoPhepGvNhapSuaDiem,
            l.SiSoHienTai,
            ConCho = l.SiSoToiDa - l.SiSoHienTai
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(string id)
    {
        var item = await _db.LopHocPhans.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (item == null) return NotFound();

        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        // RBAC: Giáo viên chỉ xem được lớp mình dạy
        if (currentRole == "giaovien")
        {
            if (item.GiangVien?.MaGV != currentRefId)
                return Forbid("Bạn không có quyền xem thông tin lớp này.");
        }

        var mon = await _db.MonHocs.Find(m => m.Id == item.MaMon).FirstOrDefaultAsync();
        int soTinChi = item.SoTinChi > 0 ? item.SoTinChi : (mon?.SoTinChi ?? 3);

        return Ok(new
        {
            item.Id,
            item.MaMon,
            item.TenMon,
            SoTinChi = soTinChi,
            item.Hocky,
            item.NamHoc,
            item.GiangVien,
            item.PhongHoc,
            item.LichHoc,
            item.SiSoToiDa,
            item.DaMo,
            item.ChoPhepGvNhapSuaDiem,
            item.SiSoHienTai,
            ConCho = item.SiSoToiDa - item.SiSoHienTai
        });
    }

    /// <summary>Lấy danh sách sinh viên trong lớp học phần (kèm điểm)</summary>
    [HttpGet("{id}/sinhvien")]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult> GetSinhVienInClass(string id)
    {
        var lhp = await _db.LopHocPhans.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (lhp == null) return NotFound("Không tìm thấy lớp học phần.");

        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        // RBAC: Giáo viên chỉ xem sinh viên trong lớp mình dạy
        if (currentRole == "giaovien")
        {
            if (lhp.GiangVien?.MaGV != currentRefId)
                return Forbid("Bạn không có quyền xem thông tin lớp này.");
        }

        var filter = Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == id);
        var students = await _db.SinhViens.Find(filter).ToListAsync();

        var result = students.Select(s =>
        {
            var diem = s.BangDiem.FirstOrDefault(b => b.MaLHP == id);
            return new
            {
                s.Id,
                s.HoTen,
                s.LopSinhHoat,
                s.Email,
                BangDiem = diem
            };
        }).OrderBy(s => s.Id).ToList();

        return Ok(new { lopHocPhan = lhp, sinhViens = result });
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Create(LopHocPhan lhp)
    {
        lhp.SiSoHienTai = 0;
        await _db.LopHocPhans.InsertOneAsync(lhp);
        return CreatedAtAction(nameof(GetById), new { id = lhp.Id }, lhp);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(string id, LopHocPhan lhp)
    {
        var existing = await _db.LopHocPhans.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (existing == null) return NotFound();

        var registeredCount = await _db.SinhViens.CountDocumentsAsync(
            x => x.BangDiem.Any(b => b.MaLHP == id));
        var enrollmentCount = Math.Max(registeredCount, existing.SiSoHienTai);

        if (lhp.SiSoToiDa < enrollmentCount && lhp.SiSoToiDa < existing.SiSoToiDa)
            return Conflict($"S\u1ec9 s\u1ed1 t\u1ed1i \u0111a kh\u00f4ng th\u1ec3 th\u1ea5p h\u01a1n s\u1ed1 sinh vi\u00ean \u0111ang \u0111\u0103ng k\u00fd ({enrollmentCount}).");
        if (lhp.SiSoToiDa <= 0)
            return BadRequest("S\u1ec9 s\u1ed1 t\u1ed1i \u0111a ph\u1ea3i l\u1edbn h\u01a1n 0.");
        if (string.IsNullOrWhiteSpace(lhp.MaMon) || string.IsNullOrWhiteSpace(lhp.Hocky) ||
            string.IsNullOrWhiteSpace(lhp.NamHoc) || string.IsNullOrWhiteSpace(lhp.GiangVien?.MaGV))
            return BadRequest("M\u00e3 m\u00f4n, h\u1ecdc k\u1ef3, n\u0103m h\u1ecdc v\u00e0 gi\u1ea3ng vi\u00ean l\u00e0 b\u1eaft bu\u1ed9c.");

        if (enrollmentCount > 0 &&
            (!string.Equals(existing.MaMon, lhp.MaMon, StringComparison.Ordinal) ||
             !string.Equals(existing.Hocky, lhp.Hocky, StringComparison.Ordinal) ||
             !string.Equals(existing.NamHoc, lhp.NamHoc, StringComparison.Ordinal)))
        {
            return Conflict("L\u1edbp \u0111\u00e3 c\u00f3 sinh vi\u00ean \u0111\u0103ng k\u00fd; kh\u00f4ng th\u1ec3 \u0111\u1ed5i m\u00f4n h\u1ecdc, h\u1ecdc k\u1ef3 ho\u1eb7c n\u0103m h\u1ecdc.");
        }

        var monHoc = await _db.MonHocs.Find(x => x.Id == lhp.MaMon).FirstOrDefaultAsync();
        if (monHoc == null) return BadRequest("M\u00f4n h\u1ecdc kh\u00f4ng t\u1ed3n t\u1ea1i.");
        var giangVien = await _db.GiangViens.Find(x => x.Id == lhp.GiangVien.MaGV).FirstOrDefaultAsync();
        if (giangVien == null) return BadRequest("Gi\u1ea3ng vi\u00ean kh\u00f4ng t\u1ed3n t\u1ea1i.");

        // Course name/credits are canonical values from MonHoc, never user supplied copies.
        lhp.Id = id;
        lhp.TenMon = enrollmentCount > 0 ? existing.TenMon : monHoc.TenMon;
        lhp.SoTinChi = enrollmentCount > 0 ? existing.SoTinChi : monHoc.SoTinChi;
        lhp.GiangVien = new GiangVienPhuTrach
        {
            MaGV = giangVien.Id,
            HoTen = giangVien.HoTen,
            TenGV = null
        };

        var instructorChanged = existing.GiangVien?.MaGV != lhp.GiangVien.MaGV;
        var studentFilter = Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == id);
        var studentUpdate = Builders<SinhVien>.Update.Set("BangDiem.$[entry].GiangVien", lhp.GiangVien);
        var studentUpdateOptions = new UpdateOptions
        {
            ArrayFilters = new List<ArrayFilterDefinition>
            {
                new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("entry.MaLHP", id))
            }
        };

        // Persist the denormalized teacher first. If either write fails, compensate so
        // the class and its enrollment snapshots remain aligned without touching grades.
        if (instructorChanged && registeredCount > 0)
        {
            try
            {
                await _db.SinhViens.UpdateManyAsync(studentFilter, studentUpdate, studentUpdateOptions);
            }
            catch
            {
                try
                {
                    await _db.SinhViens.UpdateManyAsync(
                        studentFilter,
                        Builders<SinhVien>.Update.Set("BangDiem.$[entry].GiangVien", existing.GiangVien),
                        studentUpdateOptions);
                    return StatusCode(500, "Kh\u00f4ng th\u1ec3 \u0111\u1ed5i gi\u1ea3ng vi\u00ean; d\u1eef li\u1ec7u b\u1ea3ng \u0111i\u1ec3m \u0111\u00e3 \u0111\u01b0\u1ee3c kh\u00f4i ph\u1ee5c.");
                }
                catch
                {
                    return StatusCode(500, "L\u1ed7i khi \u0111\u1ed5i gi\u1ea3ng vi\u00ean; c\u1ea7n ki\u1ec3m tra l\u1ea1i c\u00e1c b\u1ea3n ghi gi\u1ea3ng vi\u00ean trong b\u1ea3ng \u0111i\u1ec3m.");
                }
            }
        }

        var update = Builders<LopHocPhan>.Update
            .Set(x => x.MaMon, lhp.MaMon)
            .Set(x => x.TenMon, lhp.TenMon)
            .Set(x => x.SoTinChi, lhp.SoTinChi)
            .Set(x => x.Hocky, lhp.Hocky)
            .Set(x => x.NamHoc, lhp.NamHoc)
            .Set(x => x.GiangVien, lhp.GiangVien)
            .Set(x => x.PhongHoc, lhp.PhongHoc)
            .Set(x => x.LichHoc, lhp.LichHoc)
            .Set(x => x.SiSoToiDa, lhp.SiSoToiDa)
            .Set(x => x.DaMo, lhp.DaMo)
            .Set(x => x.ChoPhepGvNhapSuaDiem, lhp.ChoPhepGvNhapSuaDiem);

        try
        {
            var result = await _db.LopHocPhans.UpdateOneAsync(x => x.Id == id, update);
            if (result.MatchedCount > 0) return NoContent();
        }
        catch
        {
            if (instructorChanged && registeredCount > 0)
            {
                try
                {
                    await _db.SinhViens.UpdateManyAsync(
                        studentFilter,
                        Builders<SinhVien>.Update.Set("BangDiem.$[entry].GiangVien", existing.GiangVien),
                        studentUpdateOptions);
                }
                catch
                {
                    return StatusCode(500, "L\u1ed7i l\u01b0u l\u1edbp h\u1ecdc ph\u1ea7n v\u00e0 kh\u00f4i ph\u1ee5c ph\u00e2n c\u00f4ng; c\u1ea7n ki\u1ec3m tra l\u1ea1i d\u1eef li\u1ec7u.");
                }
            }
            return StatusCode(500, "Kh\u00f4ng th\u1ec3 l\u01b0u th\u00f4ng tin l\u1edbp h\u1ecdc ph\u1ea7n.");
        }

        if (instructorChanged && registeredCount > 0)
        {
            try
            {
                await _db.SinhViens.UpdateManyAsync(
                    studentFilter,
                    Builders<SinhVien>.Update.Set("BangDiem.$[entry].GiangVien", existing.GiangVien),
                    studentUpdateOptions);
            }
            catch
            {
                return StatusCode(500, "Kh\u00f4ng t\u00ecm th\u1ea5y l\u1edbp khi l\u01b0u; b\u1ea3n ghi gi\u1ea3ng vi\u00ean trong b\u1ea3ng \u0111i\u1ec3m c\u1ea7n \u0111\u01b0\u1ee3c ki\u1ec3m tra.");
            }
        }
        return NotFound();
    }

    /// <summary>
    /// Bật/tắt trạng thái mở-đóng lớp học phần cho sinh viên đăng ký.
    /// true = mở (cho phép đăng ký), false = đóng (không cho đăng ký).
    /// </summary>
    [HttpPut("{id}/trangthai")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ToggleTrangThai(string id, [FromBody] TrangThaiLhp request)
    {
        var lhp = await _db.LopHocPhans.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (lhp == null) return NotFound("Không tìm thấy lớp học phần.");

        var updateResult = await _db.LopHocPhans.UpdateOneAsync(
            x => x.Id == id,
            Builders<LopHocPhan>.Update.Set(x => x.DaMo, request.DaMo));

        if (updateResult.MatchedCount == 0) return NotFound();

        return Ok(new { lhp.Id, DaMo = request.DaMo, Message = request.DaMo ? "Đã mở lớp học phần." : "Đã đóng lớp học phần." });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var lhp = await _db.LopHocPhans.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (lhp == null) return NotFound();

        var enrollmentCount = await _db.SinhViens.CountDocumentsAsync(x => x.BangDiem.Any(b => b.MaLHP == id));
        if (enrollmentCount > 0)
            return Conflict("Không thể xóa lớp học phần vì vẫn còn sinh viên đăng ký.");

        var result = await _db.LopHocPhans.DeleteOneAsync(
            Builders<LopHocPhan>.Filter.Eq(x => x.Id, id) &
            Builders<LopHocPhan>.Filter.Eq(x => x.SiSoHienTai, 0));
        if (result.DeletedCount > 0) return NoContent();

        var stillExists = await _db.LopHocPhans.Find(x => x.Id == id).AnyAsync();
        return stillExists
            ? Conflict("Không thể xóa lớp học phần vì vẫn còn sinh viên đăng ký.")
            : NotFound();
    }

    /// <summary>
    /// Lấy danh sách lớp học phần cho dropdown filter (chỉ lớp giáo viên dạy)
    /// </summary>
    [HttpGet("dropdown")]
    [Authorize(Roles = "admin,giaovien")]
    public async Task<ActionResult> GetForDropdown()
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        var filter = Builders<LopHocPhan>.Filter.Empty;

        // RBAC: Giáo viên chỉ thấy các lớp mình dạy
        if (currentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            filter &= Builders<LopHocPhan>.Filter.Eq("GiangVien.MaGV", currentRefId);
        }

        var lopHocPhans = await _db.LopHocPhans
            .Find(filter)
            .Project(x => new 
            {
                x.Id,
                x.TenMon,
                x.Hocky,
                x.NamHoc,
                x.GiangVien,
                x.DaMo,
                x.ChoPhepGvNhapSuaDiem
            })
            .SortBy(x => x.TenMon)
            .ThenBy(x => x.Hocky)
            .ThenBy(x => x.NamHoc)
            .ToListAsync();

        var result = lopHocPhans.Select(l => new
        {
            Value = l.Id,
            Text = $"{l.TenMon} - {l.Hocky}/{l.NamHoc}",
            l.Id,
            l.TenMon,
            l.Hocky,
            l.NamHoc,
            l.DaMo,
            l.ChoPhepGvNhapSuaDiem
        }).ToList();

        return Ok(result);
    }
}
