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

        // RBAC: Sinh viên và giáo viên chỉ xem được giảng viên cùng khoa
        if (currentRole == "sinhvien" || currentRole == "giaovien")
        {
            var currentRefId = User.FindFirstValue("RefId");
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã.");

            var userGv = await _db.GiangViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (userGv == null)
                return NotFound("Không tìm thấy thông tin người dùng.");

            // Admin có thể xem tất cả, giáo viên chỉ xem cùng khoa
            if (currentRole == "giaovien" && userGv.MaKhoa != id)
                return Forbid("Bạn không có quyền xem thông tin giảng viên này.");
        }

        var gv = await _db.GiangViens.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (gv == null) return NotFound();
        return gv;
    }
}
