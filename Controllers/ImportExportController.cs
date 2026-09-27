using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using System.Text.Json;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class ImportExportController : ControllerBase
{
    private readonly MongoDbService _db;

    public ImportExportController(MongoDbService db) => _db = db;

    [HttpGet("sinhvien")]
    public async Task<IActionResult> ExportSinhVien([FromQuery] string? lop)
    {
        var filter = string.IsNullOrWhiteSpace(lop)
            ? FilterDefinition<SinhVien>.Empty
            : Builders<SinhVien>.Filter.Eq(x => x.LopSinhHoat, lop);

        var list = await _db.SinhViens.Find(filter).ToListAsync();
        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = string.IsNullOrWhiteSpace(lop) ? "bangdiem_sinhvien.json" : $"bangdiem_{lop}.json";
        return File(bytes, "application/json", fileName);
    }

    [HttpPost("sinhvien")]
    public async Task<IActionResult> ImportSinhVien(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Chưa chọn file JSON.");

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();

        List<SinhVien>? students;
        try
        {
            students = JsonSerializer.Deserialize<List<SinhVien>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return BadRequest($"JSON không hợp lệ: {ex.Message}");
        }

        if (students is null || students.Count == 0)
            return BadRequest("File không chứa sinh viên.");

        var ops = students.Select(sv =>
            new ReplaceOneModel<SinhVien>(Builders<SinhVien>.Filter.Eq(x => x.Id, sv.Id), sv)
            {
                IsUpsert = true
            }).ToList();

        var result = await _db.SinhViens.BulkWriteAsync(ops);
        return Ok(new
        {
            imported = students.Count,
            upserted = result.Upserts.Count,
            modified = result.ModifiedCount
        });
    }
}
