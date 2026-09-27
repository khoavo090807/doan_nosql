using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class TaiKhoanController : ControllerBase
{
    private readonly MongoDbService _db;

    public TaiKhoanController(MongoDbService db)
    {
        _db = db;
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

        var existing = await _db.TaiKhoans.Find(x => x.Id == req.Id.Trim()).FirstOrDefaultAsync();
        if (existing != null)
            return Conflict($"Tài khoản '{req.Id}' đã tồn tại.");

        var newAcc = new TaiKhoan
        {
            Id = req.Id.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = req.Role.Trim().ToLower(),
            RefId = string.IsNullOrWhiteSpace(req.RefId) ? null : req.RefId.Trim(),
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

        var update = Builders<TaiKhoan>.Update
            .Set(x => x.Role, req.Role.Trim().ToLower())
            .Set(x => x.RefId, string.IsNullOrWhiteSpace(req.RefId) ? null : req.RefId.Trim())
            .Set(x => x.HoTen, req.HoTen.Trim())
            .Set(x => x.IsActive, req.IsActive);

        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            update = update.Set(x => x.PasswordHash, BCrypt.Net.BCrypt.HashPassword(req.Password));
        }

        await _db.TaiKhoans.UpdateOneAsync(x => x.Id == id, update);
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

        return Ok("Đã xóa tài khoản thành công.");
    }
}
