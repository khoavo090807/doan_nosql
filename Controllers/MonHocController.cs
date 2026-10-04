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
public class MonHocController : ControllerBase
{
    private readonly MongoDbService _db;

    public MonHocController(MongoDbService db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<MonHoc>>> Get([FromQuery] string? maKhoa)
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        var filter = Builders<MonHoc>.Filter.Empty;

        // RBAC: Giáo viên và sinh viên chỉ xem môn học của khoa mình
        if (currentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            var gv = await _db.GiangViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (gv == null)
                return NotFound("Không tìm thấy thông tin giảng viên.");

            filter &= Builders<MonHoc>.Filter.Eq(x => x.MaKhoa, gv.MaKhoa);
        }
        else if (currentRole == "sinhvien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã sinh viên.");

            var sv = await _db.SinhViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (sv == null)
                return NotFound("Không tìm thấy thông tin sinh viên.");

            filter &= Builders<MonHoc>.Filter.Eq(x => x.MaKhoa, sv.MaKhoa);
        }
        // Admin có thể filter theo maKhoa hoặc xem tất cả
        else if (!string.IsNullOrWhiteSpace(maKhoa))
        {
            filter &= Builders<MonHoc>.Filter.Eq(x => x.MaKhoa, maKhoa.Trim());
        }

        return await _db.MonHocs.Find(filter).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MonHoc>> GetById(string id)
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        // Lấy môn học trước
        var monHoc = await _db.MonHocs.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (monHoc == null) return NotFound();

        // RBAC: Sinh viên và giáo viên chỉ xem môn học của khoa mình
        if (currentRole == "sinhvien" || currentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã.");

            if (currentRole == "sinhvien")
            {
                var userSv = await _db.SinhViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
                if (userSv == null)
                    return NotFound("Không tìm thấy thông tin sinh viên.");
                
                if (monHoc.MaKhoa != userSv.MaKhoa)
                    return Forbid("Bạn không có quyền xem môn học này.");
            }
            else if (currentRole == "giaovien")
            {
                var userGv = await _db.GiangViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
                if (userGv == null)
                    return NotFound("Không tìm thấy thông tin giảng viên.");
                
                if (monHoc.MaKhoa != userGv.MaKhoa)
                    return Forbid("Bạn không có quyền xem môn học này.");
            }
        }

        return monHoc;
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<MonHoc>> Create([FromBody] MonHoc monHoc)
    {
        if (monHoc == null)
            return BadRequest("Dữ liệu không hợp lệ.");

        if (string.IsNullOrWhiteSpace(monHoc.Id))
            return BadRequest("Mã môn học không được để trống.");

        monHoc.Id = monHoc.Id.Trim();
        var existing = await _db.MonHocs.Find(x => x.Id == monHoc.Id).FirstOrDefaultAsync();
        if (existing != null)
            return BadRequest($"Mã môn học '{monHoc.Id}' đã tồn tại.");

        if (string.IsNullOrWhiteSpace(monHoc.TenMon))
            return BadRequest("Tên môn học không được để trống.");

        if (monHoc.SoTinChi <= 0)
            return BadRequest("Số tín chỉ phải lớn hơn 0.");

        if (monHoc.LyThuyet < 0 || monHoc.ThucHanh < 0)
            return BadRequest("Số tiết lý thuyết và thực hành không được âm.");

        if (string.IsNullOrWhiteSpace(monHoc.MaKhoa))
            return BadRequest("Mã khoa không được để trống.");

        var khoaExists = await _db.Khoas.Find(x => x.Id == monHoc.MaKhoa.Trim()).AnyAsync();
        if (!khoaExists)
            return BadRequest($"Mã khoa '{monHoc.MaKhoa}' không tồn tại.");

        if (monHoc.CLO != null && monHoc.CLO.Any())
        {
            if (monHoc.CLO.Any(c => string.IsNullOrWhiteSpace(c.MaCLO)))
                return BadRequest("Mã CLO không được để trống.");

            var cloIds = monHoc.CLO.Select(c => c.MaCLO.Trim()).ToList();
            if (cloIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != cloIds.Count)
                return BadRequest("Mã CLO trong môn học không được trùng nhau.");
        }

        monHoc.TenMon = monHoc.TenMon.Trim();
        monHoc.MaKhoa = monHoc.MaKhoa.Trim();

        await _db.MonHocs.InsertOneAsync(monHoc);
        return CreatedAtAction(nameof(GetById), new { id = monHoc.Id }, monHoc);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(string id, [FromBody] MonHoc monHoc)
    {
        if (monHoc == null)
            return BadRequest("Dữ liệu không hợp lệ.");

        var existing = await _db.MonHocs.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (existing == null)
            return NotFound("Không tìm thấy môn học.");

        if (string.IsNullOrWhiteSpace(monHoc.TenMon))
            return BadRequest("Tên môn học không được để trống.");

        if (monHoc.SoTinChi <= 0)
            return BadRequest("Số tín chỉ phải lớn hơn 0.");

        if (monHoc.LyThuyet < 0 || monHoc.ThucHanh < 0)
            return BadRequest("Số tiết lý thuyết và thực hành không được âm.");

        if (string.IsNullOrWhiteSpace(monHoc.MaKhoa))
            return BadRequest("Mã khoa không được để trống.");

        var khoaExists = await _db.Khoas.Find(x => x.Id == monHoc.MaKhoa.Trim()).AnyAsync();
        if (!khoaExists)
            return BadRequest($"Mã khoa '{monHoc.MaKhoa}' không tồn tại.");

        if (monHoc.CLO != null && monHoc.CLO.Any())
        {
            if (monHoc.CLO.Any(c => string.IsNullOrWhiteSpace(c.MaCLO)))
                return BadRequest("Mã CLO không được để trống.");

            var cloIds = monHoc.CLO.Select(c => c.MaCLO.Trim()).ToList();
            if (cloIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != cloIds.Count)
                return BadRequest("Mã CLO trong môn học không được trùng nhau.");
        }

        monHoc.Id = id;
        monHoc.TenMon = monHoc.TenMon.Trim();
        monHoc.MaKhoa = monHoc.MaKhoa.Trim();

        var result = await _db.MonHocs.ReplaceOneAsync(x => x.Id == id, monHoc);
        if (result.MatchedCount == 0)
            return NotFound("Không tìm thấy môn học.");

        // Đồng bộ nếu đổi TenMon hoặc SoTinChi
        if (existing.TenMon != monHoc.TenMon || existing.SoTinChi != monHoc.SoTinChi)
        {
            // 1. Đồng bộ sang lophocphan
            var lhpFilter = Builders<LopHocPhan>.Filter.Eq(x => x.MaMon, id);
            var lhpUpdate = Builders<LopHocPhan>.Update
                .Set(x => x.TenMon, monHoc.TenMon)
                .Set(x => x.SoTinChi, monHoc.SoTinChi);
            await _db.LopHocPhans.UpdateManyAsync(lhpFilter, lhpUpdate);

            // 2. Đồng bộ sang BangDiem của sinhvien
            var svFilter = Builders<SinhVien>.Filter.ElemMatch(x => x.BangDiem, b => b.MaMon == id);
            var svUpdate = Builders<SinhVien>.Update
                .Set("BangDiem.$[elem].TenMon", monHoc.TenMon)
                .Set("BangDiem.$[elem].SoTinChi", monHoc.SoTinChi);

            var arrayFilters = new List<ArrayFilterDefinition>
            {
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("elem.MaMon", id))
            };

            await _db.SinhViens.UpdateManyAsync(
                svFilter, svUpdate,
                new UpdateOptions { ArrayFilters = arrayFilters });
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _db.MonHocs.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (existing == null)
            return NotFound("Không tìm thấy môn học.");

        var hasLhp = await _db.LopHocPhans.Find(x => x.MaMon == id).AnyAsync();
        if (hasLhp)
        {
            return StatusCode(409, "Không thể xóa môn học này vì đã có lớp học phần đang sử dụng.");
        }

        var result = await _db.MonHocs.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0 ? NoContent() : NotFound("Không tìm thấy môn học.");
    }
}
