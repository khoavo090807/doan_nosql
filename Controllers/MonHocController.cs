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
}
