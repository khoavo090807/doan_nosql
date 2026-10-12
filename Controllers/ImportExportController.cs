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

        var importedCount = students.Count;
        var studentsToImport = students.GroupBy(x => x.Id).Select(x => x.Last()).ToList();
        var studentIds = studentsToImport.Select(x => x.Id).ToList();
        var oldStudents = await _db.SinhViens.Find(x => studentIds.Contains(x.Id)).ToListAsync();
        var oldById = oldStudents.ToDictionary(x => x.Id);
        var enrollmentDeltas = new Dictionary<string, int>();

        foreach (var student in studentsToImport)
        {
            if (oldById.TryGetValue(student.Id, out var oldStudent))
            {
                foreach (var enrollment in (oldStudent.BangDiem ?? new List<BangDiemMonHoc>()).GroupBy(x => x.MaLHP))
                    enrollmentDeltas[enrollment.Key] = enrollmentDeltas.GetValueOrDefault(enrollment.Key) - enrollment.Count();
            }

            foreach (var enrollment in (student.BangDiem ?? new List<BangDiemMonHoc>()).GroupBy(x => x.MaLHP))
                enrollmentDeltas[enrollment.Key] = enrollmentDeltas.GetValueOrDefault(enrollment.Key) + enrollment.Count();
        }

        var ops = studentsToImport.Select(sv =>
            new ReplaceOneModel<SinhVien>(Builders<SinhVien>.Filter.Eq(x => x.Id, sv.Id), sv)
            {
                IsUpsert = true
            }).ToList();

        var result = await _db.SinhViens.BulkWriteAsync(ops);
        foreach (var delta in enrollmentDeltas.Where(x => x.Value != 0))
        {
            var filter = Builders<LopHocPhan>.Filter.Eq(x => x.Id, delta.Key);
            if (delta.Value < 0)
                filter &= Builders<LopHocPhan>.Filter.Gte(x => x.SiSoHienTai, -delta.Value);

            await _db.LopHocPhans.UpdateOneAsync(
                filter,
                Builders<LopHocPhan>.Update.Inc(x => x.SiSoHienTai, delta.Value));
        }

        return Ok(new
        {
            imported = importedCount,
            upserted = result.Upserts.Count,
            modified = result.ModifiedCount
        });
    }
}
