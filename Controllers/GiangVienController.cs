using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using MongoDB.Driver;
using System.Security.Claims;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GiangVienController : ControllerBase
{
    private readonly MongoDbService _db;

    public GiangVienController(MongoDbService db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<GiangVien>>> Get([FromQuery] string? maKhoa)
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        var filter = Builders<GiangVien>.Filter.Empty;

        // RBAC: Giáo viên chỉ xem thông tin giảng viên cùng khoa
        if (currentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            var gv = await _db.GiangViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (gv == null)
                return NotFound("Không tìm thấy thông tin giảng viên.");

            filter &= Builders<GiangVien>.Filter.Eq(x => x.MaKhoa, gv.MaKhoa);
        }
        // Admin có thể filter theo maKhoa hoặc xem tất cả
        else if (!string.IsNullOrWhiteSpace(maKhoa))
        {
            filter &= Builders<GiangVien>.Filter.Eq(x => x.MaKhoa, maKhoa.Trim());
        }

        return await _db.GiangViens.Find(filter).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GiangVien>> GetById(string id)
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        var targetGv = await _db.GiangViens.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (targetGv == null) return NotFound("Không tìm thấy thông tin giảng viên.");

        // RBAC: Giáo viên và sinh viên chỉ xem được giảng viên cùng khoa
        if (currentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            var currentGv = await _db.GiangViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (currentGv == null)
                return NotFound("Không tìm thấy thông tin giảng viên đang đăng nhập.");

            if (!string.Equals(currentGv.MaKhoa, targetGv.MaKhoa, StringComparison.OrdinalIgnoreCase))
                return Forbid("Bạn không có quyền xem thông tin giảng viên ngoài khoa.");
        }
        else if (currentRole == "sinhvien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã sinh viên.");

            var currentSv = await _db.SinhViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (currentSv == null)
                return NotFound("Không tìm thấy thông tin sinh viên đang đăng nhập.");

            if (!string.Equals(currentSv.MaKhoa, targetGv.MaKhoa, StringComparison.OrdinalIgnoreCase))
                return Forbid("Bạn không có quyền xem thông tin giảng viên ngoài khoa.");
        }

        return targetGv;
    }
}
