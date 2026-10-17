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
        if (studentsToImport.Any(x => string.IsNullOrWhiteSpace(x.Id)))
            return BadRequest("Mỗi hồ sơ trong JSON phải có mã sinh viên.");
        foreach (var student in studentsToImport)
        {
            var ids = (student.BangDiem ?? new List<BangDiemMonHoc>()).Select(x => x.MaLHP).ToList();
            if (ids.Count != ids.Distinct().Count())
                return BadRequest($"Sinh viên {student.Id} có đăng ký lớp học phần trùng trong JSON.");
        }
        var studentIds = studentsToImport.Select(x => x.Id).ToList();
        var oldStudents = await _db.SinhViens.Find(x => studentIds.Contains(x.Id)).ToListAsync();
        var oldById = oldStudents.ToDictionary(x => x.Id);
        var enrollmentDeltas = new Dictionary<string, int>();

        foreach (var student in studentsToImport)
        {
            if (oldById.TryGetValue(student.Id, out var oldStudent))
            {
                foreach (var maLhp in (oldStudent.BangDiem ?? new List<BangDiemMonHoc>()).Select(x => x.MaLHP).Distinct())
                    enrollmentDeltas[maLhp] = enrollmentDeltas.GetValueOrDefault(maLhp) - 1;
            }

            foreach (var maLhp in (student.BangDiem ?? new List<BangDiemMonHoc>()).Select(x => x.MaLHP).Distinct())
                enrollmentDeltas[maLhp] = enrollmentDeltas.GetValueOrDefault(maLhp) + 1;
        }

        var currentCounts = await _db.GetEnrollmentCountsAsync();
        var touchedIds = enrollmentDeltas.Keys.ToList();
        var touchedClasses = await _db.LopHocPhans.Find(x => touchedIds.Contains(x.Id)).ToListAsync();
        var classById = touchedClasses.ToDictionary(x => x.Id);
        foreach (var maLhp in touchedIds)
        {
            if (!classById.TryGetValue(maLhp, out var lhp))
                return BadRequest($"Không tìm thấy lớp học phần {maLhp} trong dữ liệu JSON.");
            var projected = currentCounts.GetValueOrDefault(maLhp) + enrollmentDeltas[maLhp];
            if (projected > lhp.SiSoToiDa && projected > currentCounts.GetValueOrDefault(maLhp))
                return Conflict($"Import sẽ vượt sĩ số tối đa lớp {maLhp}.");
        }

        var ops = studentsToImport.Select(sv =>
            new ReplaceOneModel<SinhVien>(Builders<SinhVien>.Filter.Eq(x => x.Id, sv.Id), sv)
            {
                IsUpsert = true
            }).ToList();

        BulkWriteResult<SinhVien> result;
        try
        {
            result = await _db.SinhViens.BulkWriteAsync(ops);
        }
        catch
        {
            await DongBoSiSoAsync(touchedIds);
            throw;
        }
        await DongBoSiSoAsync(touchedIds);

        return Ok(new
        {
            imported = importedCount,
            upserted = result.Upserts.Count,
            modified = result.ModifiedCount
        });
    }

    private async Task DongBoSiSoAsync(IEnumerable<string> maLhpIds)
    {
        var actualCounts = await _db.GetEnrollmentCountsAsync();
        foreach (var maLhp in maLhpIds)
            await _db.LopHocPhans.UpdateOneAsync(
                x => x.Id == maLhp,
                Builders<LopHocPhan>.Update.Set(x => x.SiSoHienTai, actualCounts.GetValueOrDefault(maLhp)));
    }
}
