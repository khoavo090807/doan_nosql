using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using System.Security.Claims;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class TaiKhoanController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly IMemoryCache _cache;

    public TaiKhoanController(MongoDbService db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public record CreateAccountRequest(
        string Id,
        string Password,
        string Role,
        string? RefId,
        string HoTen,
        bool IsActive = true
    );

    public record UpdateAccountRequest(
        string? Password,
        string Role,
        string? RefId,
        string HoTen,
        bool IsActive
    );

    public record AccountDto(
        string Id,
        string Role,
        string? RefId,
        string HoTen,
        bool IsActive
    );

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAll([FromQuery] string? role, [FromQuery] string? q)
    {
        var filter = Builders<TaiKhoan>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(role))
            filter &= Builders<TaiKhoan>.Filter.Eq(x => x.Role, role.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(q))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(q.Trim(), "i");
            filter &= Builders<TaiKhoan>.Filter.Or(
                Builders<TaiKhoan>.Filter.Regex(x => x.Id, regex),
                Builders<TaiKhoan>.Filter.Regex(x => x.HoTen, regex),
                Builders<TaiKhoan>.Filter.Regex(x => x.RefId, regex)
            );
        }

        var list = await _db.TaiKhoans.Find(filter).ToListAsync();
        var dtos = list.Select(x => new AccountDto(
            Id: x.Id,
            Role: x.Role,
            RefId: x.RefId,
            HoTen: x.HoTen,
            IsActive: x.IsActive
        )).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountDto>> GetById(string id)
    {
        var acc = await _db.TaiKhoans.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (acc == null) return NotFound("Không tìm thấy tài khoản.");

        return Ok(new AccountDto(
            Id: acc.Id,
            Role: acc.Role,
            RefId: acc.RefId,
            HoTen: acc.HoTen,
            IsActive: acc.IsActive
        ));
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateAccountRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Id) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Tên đăng nhập và mật khẩu không được để trống.");

        if (req.Password.Trim().Length < 6)
            return BadRequest("Mật khẩu phải có tối thiểu 6 ký tự.");

        var role = req.Role?.Trim().ToLower();
        var validRoles = new[] { "admin", "giaovien", "sinhvien" };
        if (string.IsNullOrWhiteSpace(role) || !validRoles.Contains(role))
            return BadRequest("Vai trò không hợp lệ. Vai trò phải là 'admin', 'giaovien' hoặc 'sinhvien'.");

        string? refId = string.IsNullOrWhiteSpace(req.RefId) ? null : req.RefId.Trim();

        if (role == "admin")
        {
            refId = null;
        }
        else if (role == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(refId))
                return BadRequest("Tài khoản giáo viên yêu cầu phải chọn Mã giảng viên (RefId).");

            var gvExists = await _db.GiangViens.Find(x => x.Id == refId).AnyAsync();
            if (!gvExists)
                return BadRequest($"Mã giảng viên '{refId}' không tồn tại trong hệ thống.");
        }
        else if (role == "sinhvien")
        {
            if (string.IsNullOrWhiteSpace(refId))
                return BadRequest("Tài khoản sinh viên yêu cầu phải chọn Mã sinh viên (RefId).");

            var svExists = await _db.SinhViens.Find(x => x.Id == refId).AnyAsync();
            if (!svExists)
                return BadRequest($"Mã sinh viên '{refId}' không tồn tại trong hệ thống.");
        }

        var existing = await _db.TaiKhoans.Find(x => x.Id == req.Id.Trim()).FirstOrDefaultAsync();
        if (existing != null)
            return Conflict($"Tài khoản '{req.Id}' đã tồn tại.");

        var newAcc = new TaiKhoan
        {
            Id = req.Id.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = role,
            RefId = refId,
            HoTen = req.HoTen.Trim(),
            IsActive = req.IsActive
        };

        await _db.TaiKhoans.InsertOneAsync(newAcc);
        return CreatedAtAction(nameof(GetById), new { id = newAcc.Id }, new AccountDto(
            Id: newAcc.Id,
            Role: newAcc.Role,
            RefId: newAcc.RefId,
            HoTen: newAcc.HoTen,
            IsActive: newAcc.IsActive
        ));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] UpdateAccountRequest req)
    {
        var acc = await _db.TaiKhoans.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (acc == null) return NotFound("Không tìm thấy tài khoản.");

        var role = req.Role?.Trim().ToLower();
        var validRoles = new[] { "admin", "giaovien", "sinhvien" };
        if (string.IsNullOrWhiteSpace(role) || !validRoles.Contains(role))
            return BadRequest("Vai trò không hợp lệ. Vai trò phải là 'admin', 'giaovien' hoặc 'sinhvien'.");

        if (!string.IsNullOrWhiteSpace(req.Password) && req.Password.Trim().Length < 6)
            return BadRequest("Mật khẩu mới phải có tối thiểu 6 ký tự.");

        string? refId = string.IsNullOrWhiteSpace(req.RefId) ? null : req.RefId.Trim();

        if (role == "admin")
        {
            refId = null;
        }
        else if (role == "giaovien")
        {
            if (string.IsNullOrWhiteSpace(refId))
                return BadRequest("Tài khoản giáo viên yêu cầu phải chọn Mã giảng viên (RefId).");

            var gvExists = await _db.GiangViens.Find(x => x.Id == refId).AnyAsync();
            if (!gvExists)
                return BadRequest($"Mã giảng viên '{refId}' không tồn tại trong hệ thống.");
        }
        else if (role == "sinhvien")
        {
            if (string.IsNullOrWhiteSpace(refId))
                return BadRequest("Tài khoản sinh viên yêu cầu phải chọn Mã sinh viên (RefId).");

            var svExists = await _db.SinhViens.Find(x => x.Id == refId).AnyAsync();
            if (!svExists)
                return BadRequest($"Mã sinh viên '{refId}' không tồn tại trong hệ thống.");
        }

        var update = Builders<TaiKhoan>.Update
            .Set(x => x.Role, role)
            .Set(x => x.RefId, refId)
            .Set(x => x.HoTen, req.HoTen.Trim())
            .Set(x => x.IsActive, req.IsActive);

        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            update = update.Set(x => x.PasswordHash, BCrypt.Net.BCrypt.HashPassword(req.Password));
        }

        await _db.TaiKhoans.UpdateOneAsync(x => x.Id == id, update);

        // Xóa cache tài khoản
        _cache.Remove($"user_active_{id}");

        return NoContent();
    }

    /// <summary>
    /// Vô hiệu hóa (Deactivate/Ban) hoặc Kích hoạt lại tài khoản.
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<ActionResult> ToggleStatus(string id, [FromBody] bool isActive)
    {
        var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id.Equals(currentUsername, StringComparison.OrdinalIgnoreCase) && !isActive)
            return BadRequest("Không thể tự vô hiệu hóa tài khoản của chính mình.");

        var result = await _db.TaiKhoans.UpdateOneAsync(
            x => x.Id == id,
            Builders<TaiKhoan>.Update.Set(x => x.IsActive, isActive)
        );

        if (result.MatchedCount == 0) return NotFound("Không tìm thấy tài khoản.");

        // Xóa cache tài khoản
        _cache.Remove($"user_active_{id}");

        return Ok(new { message = isActive ? "Đã kích hoạt tài khoản." : "Đã vô hiệu hóa tài khoản.", isActive });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Không thể tự xóa tài khoản của chính mình.");

        var result = await _db.TaiKhoans.DeleteOneAsync(x => x.Id == id);
        if (result.DeletedCount == 0) return NotFound("Không tìm thấy tài khoản.");

        // Xóa cache tài khoản
        _cache.Remove($"user_active_{id}");

        return Ok("Đã xóa tài khoản thành công.");
    }
}
