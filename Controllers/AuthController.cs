using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly IConfiguration _config;

    public AuthController(MongoDbService db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public record LoginRequest(string Username, string Password);

    public record LoginResponse(
        string Token,
        string Username,
        string Role,
        string? RefId,
        string HoTen
    );

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Vui lòng nhập tên đăng nhập và mật khẩu.");

        var trimmedUsername = req.Username.Trim();
        var filter = Builders<TaiKhoan>.Filter.Regex(
            x => x.Id,
            new BsonRegularExpression($"^{Regex.Escape(trimmedUsername)}$", "i")
        );

        var user = await _db.TaiKhoans.Find(filter).FirstOrDefaultAsync();

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Tên đăng nhập hoặc mật khẩu không chính xác.");

        if (!user.IsActive)
            return Unauthorized("Tài khoản của bạn đã bị vô hiệu hóa (khóa). Vui lòng liên hệ quản trị viên.");

        var token = GenerateJwtToken(user);

        return Ok(new LoginResponse(
            Token: token,
            Username: user.Id,
            Role: user.Role,
            RefId: user.RefId,
            HoTen: user.HoTen
        ));
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult GetCurrentUser()
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);
        var refId = User.FindFirstValue("RefId");
        var hoTen = User.FindFirstValue("HoTen");

        return Ok(new
        {
            Username = username,
            Role = role,
            RefId = refId,
            HoTen = hoTen
        });
    }

    public record DoiMatKhauRequest(string MatKhauCu, string MatKhauMoi);

    [Authorize]
    [HttpPost("doi-mat-khau")]
    public async Task<IActionResult> DoiMatKhau([FromBody] DoiMatKhauRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.MatKhauCu) || string.IsNullOrWhiteSpace(req.MatKhauMoi))
            return BadRequest("Vui lòng nhập đầy đủ mật khẩu cũ và mật khẩu mới.");

        if (req.MatKhauMoi.Trim().Length < 6)
            return BadRequest("Mật khẩu mới phải có tối thiểu 6 ký tự.");

        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(username))
            return Unauthorized("Không xác định được tài khoản.");

        var user = await _db.TaiKhoans.Find(x => x.Id == username).FirstOrDefaultAsync();
        if (user == null)
            return NotFound("Không tìm thấy thông tin tài khoản.");

        if (!BCrypt.Net.BCrypt.Verify(req.MatKhauCu, user.PasswordHash))
            return BadRequest("Mật khẩu cũ không chính xác.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.MatKhauMoi.Trim());
        await _db.TaiKhoans.ReplaceOneAsync(x => x.Id == username, user);

        var cache = HttpContext.RequestServices.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        cache?.Remove($"user_active_{username}");

        return Ok(new { Message = "Đổi mật khẩu thành công." });
    }

    private string GenerateJwtToken(TaiKhoan user)
    {
        var jwtKey = _config["Jwt:Key"]!;
        var jwtIssuer = _config["Jwt:Issuer"]!;
        var jwtAudience = _config["Jwt:Audience"]!;
        var expireMinutes = int.TryParse(_config["Jwt:ExpireMinutes"], out var m) ? m : 480;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.HoTen),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("HoTen", user.HoTen)
        };

        if (!string.IsNullOrEmpty(user.RefId))
            claims.Add(new Claim("RefId", user.RefId));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
