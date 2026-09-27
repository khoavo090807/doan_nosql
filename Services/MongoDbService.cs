using MongoDB.Driver;
using doan_cuoiky_nosql.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

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
}
