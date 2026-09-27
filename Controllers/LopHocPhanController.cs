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

        // Đếm sĩ số hiện tại từ BangDiem của sinh viên
        var enrollCounts = await _db.SinhViens.Aggregate<BsonDocument>(new[]
        {
            new BsonDocument("$unwind", "$BangDiem"),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$BangDiem.MaLHP" },
                { "count", new BsonDocument("$sum", 1) }
            })
        }).ToListAsync();

        var countDict = enrollCounts
            .Where(d => d.Contains("_id") && d["_id"].IsString)
            .ToDictionary(
                d => d["_id"].AsString,
                d => d["count"].AsInt32
            );

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
            SiSoHienTai = countDict.TryGetValue(l.Id, out var cnt) ? cnt : 0,
            ConCho = l.SiSoToiDa - (countDict.TryGetValue(l.Id, out var c) ? c : 0)
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

        var count = await _db.SinhViens.CountDocumentsAsync(x => x.BangDiem.Any(b => b.MaLHP == id));
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
            SiSoHienTai = (int)count,
            ConCho = item.SiSoToiDa - (int)count
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
        await _db.LopHocPhans.InsertOneAsync(lhp);
        return CreatedAtAction(nameof(GetById), new { id = lhp.Id }, lhp);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(string id, LopHocPhan lhp)
    {
        lhp.Id = id;
        var existing = await _db.LopHocPhans.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (existing == null) return NotFound();

        var result = await _db.LopHocPhans.ReplaceOneAsync(x => x.Id == id, lhp);
        if (result.MatchedCount == 0) return NotFound();

        // Đồng bộ thông tin giảng viên mới vào BangDiem của tất cả sinh viên đã đăng ký lớp này
        if (lhp.GiangVien != null)
        {
            var svFilter = Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaLHP == id);
            var svUpdate = Builders<SinhVien>.Update
                .Set("BangDiem.$[elem].GiangVien.MaGV", lhp.GiangVien.MaGV)
                .Set("BangDiem.$[elem].GiangVien.HoTen", lhp.GiangVien.HoTen);
            var arrayFilters = new List<ArrayFilterDefinition>
            {
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("elem.MaLHP", id))
            };
            var updateOptions = new UpdateOptions { ArrayFilters = arrayFilters };
            await _db.SinhViens.UpdateManyAsync(svFilter, svUpdate, updateOptions);
        }

        return NoContent();
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

        lhp.DaMo = request.DaMo;
        var updateResult = await _db.LopHocPhans.ReplaceOneAsync(
            x => x.Id == id, lhp,
            new ReplaceOptions { IsUpsert = false });

        if (updateResult.MatchedCount == 0) return NotFound();

        return Ok(new { lhp.Id, lhp.DaMo, Message = request.DaMo ? "Đã mở lớp học phần." : "Đã đóng lớp học phần." });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _db.LopHocPhans.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0 ? NoContent() : NotFound();
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
