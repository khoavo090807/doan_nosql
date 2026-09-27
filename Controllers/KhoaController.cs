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
public class KhoaController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;

    public KhoaController(MongoDbService mongoDbService)
    {
        _mongoDbService = mongoDbService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Khoa>>> Get()
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        // RBAC: Sinh viên và giáo viên chỉ xem khoa của mình
        if (currentRole == "sinhvien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã sinh viên.");

            var sv = await _mongoDbService.SinhViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (sv == null)
                return NotFound("Không tìm thấy thông tin sinh viên.");

            var khoa = await _mongoDbService.Khoas.Find(x => x.Id == sv.MaKhoa).FirstOrDefaultAsync();
            return khoa == null ? Ok(new List<Khoa>()) : Ok(new List<Khoa> { khoa });
        }
        else if (currentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            var gv = await _mongoDbService.GiangViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (gv == null)
                return NotFound("Không tìm thấy thông tin giảng viên.");

            var khoa = await _mongoDbService.Khoas.Find(x => x.Id == gv.MaKhoa).FirstOrDefaultAsync();
            return khoa == null ? Ok(new List<Khoa>()) : Ok(new List<Khoa> { khoa });
        }

        // Admin xem tất cả khoa
        return await _mongoDbService.Khoas.Find(_ => true).SortBy(x => x.Id).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Khoa>> GetById(string id)
    {
        var currentRole = User.FindFirstValue(ClaimTypes.Role);
        var currentRefId = User.FindFirstValue("RefId");

        var k = await _mongoDbService.Khoas.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (k == null) return NotFound();

        // RBAC: Sinh viên và giáo viên chỉ xem khoa của mình
        if (currentRole == "sinhvien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã sinh viên.");

            var sv = await _mongoDbService.SinhViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (sv == null)
                return NotFound("Không tìm thấy thông tin sinh viên.");

            if (sv.MaKhoa != id)
                return Forbid();
        }
        else if (currentRole == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(currentRefId))
                return BadRequest("Không xác định được mã giảng viên.");

            var gv = await _mongoDbService.GiangViens.Find(x => x.Id == currentRefId).FirstOrDefaultAsync();
            if (gv == null)
                return NotFound("Không tìm thấy thông tin giảng viên.");

            if (gv.MaKhoa != id)
                return Forbid();
        }

        return k;
    }
}
