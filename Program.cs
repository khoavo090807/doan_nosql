using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.WriteIndented = false;
    });
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

// Đồng bộ giảng viên trong BangDiem khi server khởi động
await SyncGiangVienInBangDiem(app.Services);

app.Run();

// ======= HÀM ĐỒNG BỘ GIẢNG VIÊN =======
static async Task SyncGiangVienInBangDiem(IServiceProvider services)
{
    try
    {
        var db = services.GetRequiredService<MongoDbService>();
        var allLhp = await db.LopHocPhans.Find(_ => true).ToListAsync();
        int updated = 0;

        foreach (var lhp in allLhp.Where(l => l.GiangVien != null))
        {
            var svFilter = Builders<SinhVien>.Filter
                .ElemMatch(x => x.BangDiem, b => b.MaLHP == lhp.Id);

            var svUpdate = Builders<SinhVien>.Update
                .Set("BangDiem.$[elem].GiangVien.MaGV", lhp.GiangVien!.MaGV)
                .Set("BangDiem.$[elem].GiangVien.HoTen", lhp.GiangVien!.HoTen);

            var arrayFilters = new List<ArrayFilterDefinition>
            {
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("elem.MaLHP", lhp.Id))
            };

            var result = await db.SinhViens.UpdateManyAsync(
                svFilter, svUpdate,
                new UpdateOptions { ArrayFilters = arrayFilters });

            if (result.ModifiedCount > 0) updated++;
        }

        Console.WriteLine($"[SYNC] Đồng bộ giảng viên hoàn tất: {updated}/{allLhp.Count} lớp học phần đã cập nhật.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SYNC] Lỗi đồng bộ giảng viên: {ex.Message}");
    }
}