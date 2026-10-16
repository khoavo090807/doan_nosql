# Hệ thống quản lý và chấm điểm sinh viên NoSQL

Ứng dụng web quản lý sinh viên, môn học, lớp học phần, đăng ký và kết quả học tập. Backend dùng ASP.NET Core 9 Web API và MongoDB; giao diện HTML, CSS và JavaScript được phục vụ từ `wwwroot`.

## Chức năng

- Quản lý khoa, giảng viên, môn học, sinh viên, tài khoản và lớp học phần.
- Phân quyền theo vai trò Admin, giảng viên và sinh viên.
- Sinh viên đăng ký hoặc hủy đăng ký trong thời gian lớp cho phép.
- Giảng viên xem danh sách lớp được phân công, nhập điểm và đặt lại điểm.
- Tính điểm tổng kết, điểm chữ, GPA và báo cáo học lực; xuất bảng điểm PDF.
- Import/export hồ sơ sinh viên dạng JSON.
- Admin kiểm tra và chủ động đồng bộ sĩ số từ đăng ký thực tế.

## Công nghệ

- .NET 9 / ASP.NET Core Web API
- MongoDB và MongoDB.Driver
- JWT Bearer authentication
- HTML, CSS, JavaScript và Bootstrap
- QuestPDF

## Yêu cầu

- .NET SDK 9.0
- MongoDB đang chạy cục bộ hoặc một MongoDB server có thể kết nối
- MongoDB Database Tools nếu cần nạp bộ dữ liệu mẫu bằng `mongoimport`

## Chạy ứng dụng

1. Khởi động MongoDB.
2. Tại thư mục dự án, restore package và chạy ứng dụng:

   ```powershell
   dotnet restore
   dotnet run --launch-profile http
   ```

3. Mở [http://localhost:5072](http://localhost:5072).

Mặc định ứng dụng dùng `mongodb://localhost:27017` và database `doan_cuoiky`. Có thể ghi đè cấu hình bằng biến môi trường:

```powershell
$env:MongoDbSettings__ConnectionString = "mongodb://localhost:27017"
$env:MongoDbSettings__DatabaseName = "doan_cuoiky"
$env:Jwt__Key = "replace-with-a-long-random-secret"
dotnet run --launch-profile http
```

Khi khởi động, ứng dụng tạo/xác nhận các index cần thiết, tính lại sĩ số từ đăng ký trong `BangDiem`, và đồng bộ thông tin giảng viên vào các bản ghi bảng điểm tương ứng.

## Nạp dữ liệu mẫu

Các file JSON trong `database_mongodb/` là dữ liệu mẫu cho từng collection. Chúng không được tự động nạp khi ứng dụng khởi động. Từ PowerShell, có thể upsert bộ dữ liệu vào MongoDB như sau:

```powershell
Get-ChildItem database_mongodb\doan_cuoiky.*.json | ForEach-Object {
    $collection = $_.BaseName -replace '^doan_cuoiky\.', ''
    mongoimport --uri "mongodb://localhost:27017" `
        --db doan_cuoiky `
        --collection $collection `
        --file $_.FullName `
        --jsonArray `
        --mode=upsert `
        --upsertFields=_id
}
```

Lệnh trên cập nhật document theo `_id` và giữ nguyên các document không có trong file. Các file tài khoản và mật khẩu mẫu chỉ dành cho môi trường học tập cục bộ.

## Đăng nhập mẫu

Các tài khoản demo cũng hiển thị trên trang đăng nhập:

| Vai trò | Tên đăng nhập | Mật khẩu |
| --- | --- | --- |
| Admin | `admin` | `Admin@123` |
| Giảng viên | `GV001` | `GV001@Huit` |
| Sinh viên | `SV2026001` | `SV2026001@Huit` |

Đây là thông tin demo. Hãy thay khóa JWT và đổi mật khẩu nếu triển khai ngoài máy cá nhân; không dùng cấu hình mẫu cho môi trường công khai.

## API chính

API dùng tiền tố `/api`. Hầu hết endpoint yêu cầu JWT; gọi `POST /api/Auth/login` để nhận token.

| Nhóm | Một số endpoint |
| --- | --- |
| Đăng nhập | `POST /api/Auth/login`, `GET /api/Auth/me` |
| Sinh viên và điểm | `GET /api/SinhVien`, `PUT /api/SinhVien/{id}/diem/{maLhp}`, `DELETE /api/SinhVien/{id}/diem/{maLhp}` |
| Đăng ký học phần | `POST /api/SinhVien/dangky/{maLhp}`, `DELETE /api/SinhVien/huydangky/{maLhp}` |
| Lớp học phần | `GET /api/LopHocPhan`, `GET /api/LopHocPhan/{id}/sinhvien` |
| Đối soát sĩ số (Admin) | `GET /api/LopHocPhan/siso/kiemtra`, `POST /api/LopHocPhan/siso/dongbo` |
| Import/export sinh viên (Admin) | `GET /api/ImportExport/sinhvien`, `POST /api/ImportExport/sinhvien` |
| Báo cáo | `GET /api/Reports/hocluc`, `GET /api/Reports/lopsinhhoat` |

Phân quyền được kiểm tra ở backend. Admin quản lý dữ liệu; giảng viên bị giới hạn theo lớp được phân công; sinh viên thao tác đăng ký và xem dữ liệu của chính mình.

## Mô hình dữ liệu và sĩ số

- Collection `sinhvien` lưu hồ sơ sinh viên và mảng đăng ký/điểm `BangDiem`.
- Collection `lophocphan` lưu thông tin lớp, sĩ số hiện tại và sĩ số tối đa.
- Các collection khác gồm `taikhoan`, `monhoc`, `giangvien`, `khoa`.
- Sĩ số được tính theo số sinh viên có ít nhất một đăng ký mang mã lớp đó; phần tử trùng của cùng sinh viên không làm tăng số đếm.
- Đăng ký dùng cập nhật sĩ số có điều kiện để giới hạn chỗ; hủy đăng ký kiểm tra bộ đếm trước khi xóa bản ghi. Nếu các ghi độc lập không thể hoàn tác được, Admin có thể dùng chức năng đối soát để phát hiện và đồng bộ lại sĩ số.
- Nhập, sửa hoặc đặt lại điểm không thay đổi đăng ký hay sĩ số.

## Build và kiểm thử

```powershell
dotnet build
dotnet test
```

Repository hiện có ứng dụng web, chưa có project test tự động riêng. Có thể kiểm tra API qua Swagger/OpenAPI trong môi trường Development tại `/openapi/v1.json` hoặc qua giao diện web.
