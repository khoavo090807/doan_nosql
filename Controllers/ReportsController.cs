using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using doan_cuoiky_nosql.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace doan_cuoiky_nosql.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,giaovien")]
public class ReportsController : ControllerBase
{
    private readonly MongoDbService _db;

    public ReportsController(MongoDbService db)
    {
        _db = db;
    }

    /// <summary>
    /// Báo cáo học lực theo lớp. GPA trọng số được tính hoàn toàn bằng Aggregation Pipeline
    /// ($unwind, $group, $project, $cond, $switch).
    /// </summary>
    [HttpGet("hocluc")]
    public async Task<IActionResult> BaoCaoHocLuc([FromQuery] string? lop)
    {
        var match = string.IsNullOrWhiteSpace(lop)
            ? new BsonDocument()
            : new BsonDocument("LopSinhHoat", lop);

        var pipeline = new[]
        {
            new BsonDocument("$match", match),
            new BsonDocument("$unwind", "$BangDiem"),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$_id" },
                { "HoTen", new BsonDocument("$first", "$HoTen") },
                { "LopSinhHoat", new BsonDocument("$first", "$LopSinhHoat") },
                { "KhoaHoc", new BsonDocument("$first", "$KhoaHoc") },
                {
                    "TongDiemHeSo",
                    new BsonDocument("$sum", new BsonDocument("$multiply", new BsonArray
                    {
                        "$BangDiem.DiemTongKet",
                        "$BangDiem.SoTinChi"
                    }))
                },
                { "TongTinChi", new BsonDocument("$sum", "$BangDiem.SoTinChi") }
            }),
            new BsonDocument("$project", new BsonDocument
            {
                { "HoTen", 1 },
                { "LopSinhHoat", 1 },
                { "KhoaHoc", 1 },
                { "TongTinChi", 1 },
                {
                    "GPA",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$TongTinChi", 0 }),
                        0,
                        new BsonDocument("$round", new BsonArray
                        {
                            new BsonDocument("$divide", new BsonArray { "$TongDiemHeSo", "$TongTinChi" }),
                            2
                        })
                    })
                }
            }),
            new BsonDocument("$project", new BsonDocument
            {
                { "HoTen", 1 },
                { "LopSinhHoat", 1 },
                { "KhoaHoc", 1 },
                { "TongTinChi", 1 },
                { "GPA", 1 },
                {
                    "XepLoai",
                    new BsonDocument("$switch", new BsonDocument
                    {
                        {
                            "branches",
                            new BsonArray
                            {
                                new BsonDocument
                                {
                                    { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 9.0 }) },
                                    { "then", "Xuất sắc" }
                                },
                                new BsonDocument
                                {
                                    { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 8.0 }) },
                                    { "then", "Giỏi" }
                                },
                                new BsonDocument
                                {
                                    { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 7.0 }) },
                                    { "then", "Khá" }
                                },
                                new BsonDocument
                                {
                                    { "case", new BsonDocument("$gte", new BsonArray { "$GPA", 5.0 }) },
                                    { "then", "Trung bình" }
                                }
                            }
                        },
                        { "default", "Yếu" }
                    })
                }
            }),
            new BsonDocument("$sort", new BsonDocument
            {
                { "LopSinhHoat", 1 },
                { "GPA", -1 }
            })
        };

        var result = await _db.SinhViens.Aggregate<BsonDocument>(pipeline).ToListAsync();
        var json = result.Select(d =>
        {
            var dict = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Dictionary<string, object>>(d) ?? new Dictionary<string, object>();
            if (d.Contains("_id") && !dict.ContainsKey("Id"))
            {
                dict["Id"] = d["_id"].ToString();
            }
            return dict;
        });

        return Ok(json);
    }

    [HttpGet("lopsinhhoat")]
    public async Task<IActionResult> DanhSachLop()
    {
        var lops = await _db.SinhViens.DistinctAsync<string>("LopSinhHoat", FilterDefinition<Models.SinhVien>.Empty);
        var list = await lops.ToListAsync();
        list.Sort();
        return Ok(list);
    }
}
