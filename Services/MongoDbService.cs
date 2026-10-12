using MongoDB.Driver;
using doan_cuoiky_nosql.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace doan_cuoiky_nosql.Services;

public class MongoDbService
{
    private readonly IMongoDatabase _database;

    public MongoDbService(IConfiguration configuration)
    {
        var section = configuration.GetSection("MongoDbSettings");
        var connectionString = section.GetValue<string>("ConnectionString") ?? "mongodb://localhost:27017";
        var databaseName = section.GetValue<string>("DatabaseName") ?? "doan_cuoiky";

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<SinhVien> SinhViens => _database.GetCollection<SinhVien>("sinhvien");
    public IMongoCollection<LopHocPhan> LopHocPhans => _database.GetCollection<LopHocPhan>("lophocphan");
    public IMongoCollection<MonHoc> MonHocs => _database.GetCollection<MonHoc>("monhoc");
    public IMongoCollection<GiangVien> GiangViens => _database.GetCollection<GiangVien>("giangvien");
    public IMongoCollection<Khoa> Khoas => _database.GetCollection<Khoa>("khoa");
    public IMongoCollection<TaiKhoan> TaiKhoans => _database.GetCollection<TaiKhoan>("taikhoan");
    public IMongoDatabase Database => _database;

    public async Task InitializeIndexesAndEnrollmentCountsAsync()
    {
        await CreateIndexAsync(SinhViens, Builders<SinhVien>.IndexKeys.Ascending("BangDiem.MaLHP"), "ix_sinhvien_bangdiem_malhp");
        await CreateIndexAsync(SinhViens, Builders<SinhVien>.IndexKeys.Ascending(x => x.LopSinhHoat), "ix_sinhvien_lopsinhhoat");
        await CreateIndexAsync(SinhViens, Builders<SinhVien>.IndexKeys.Ascending(x => x.MaKhoa), "ix_sinhvien_makhoa");
        await CreateIndexAsync(LopHocPhans, Builders<LopHocPhan>.IndexKeys.Ascending("GiangVien.MaGV"), "ix_lophocphan_giangvien_magv");
        await CreateIndexAsync(LopHocPhans, Builders<LopHocPhan>.IndexKeys.Ascending(x => x.MaMon), "ix_lophocphan_mamon");
        await CreateIndexAsync(MonHocs, Builders<MonHoc>.IndexKeys.Ascending(x => x.MaKhoa), "ix_monhoc_makhoa");
        await CreateIndexAsync(GiangViens, Builders<GiangVien>.IndexKeys.Ascending(x => x.MaKhoa), "ix_giangvien_makhoa");

        var enrollmentCounts = await SinhViens.Aggregate<BsonDocument>(new[]
        {
            new BsonDocument("$unwind", "$BangDiem"),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$BangDiem.MaLHP" },
                { "count", new BsonDocument("$sum", 1) }
            })
        }).ToListAsync();

        var countsByLhp = enrollmentCounts
            .Where(x => x.TryGetValue("_id", out var id) && id.IsString)
            .ToDictionary(x => x["_id"].AsString, x => x["count"].ToInt32());

        var allLhp = await LopHocPhans.Find(_ => true).Project(x => x.Id).ToListAsync();
        var updates = allLhp.Select(id => new UpdateOneModel<LopHocPhan>(
            Builders<LopHocPhan>.Filter.Eq(x => x.Id, id),
            Builders<LopHocPhan>.Update.Set(x => x.SiSoHienTai, countsByLhp.GetValueOrDefault(id))))
            .Cast<WriteModel<LopHocPhan>>()
            .ToList();

        if (updates.Count > 0)
            await LopHocPhans.BulkWriteAsync(updates);

        Console.WriteLine($"[SI SO] Đã khởi tạo sĩ số cho {allLhp.Count} lớp học phần từ BangDiem.");
    }

    private static async Task CreateIndexAsync<T>(
        IMongoCollection<T> collection,
        IndexKeysDefinition<T> keys,
        string name)
    {
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(keys, new CreateIndexOptions { Name = name }));
        Console.WriteLine($"[INDEX] Đã tạo/xác nhận index {collection.CollectionNamespace.CollectionName}.{name}.");
    }
}
