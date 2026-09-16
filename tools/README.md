# Công cụ phát triển

## UiPreview: giao diện IFC/Duct

```powershell
dotnet build tools/UiPreview/UiPreview.csproj -c Release
./tools/UiPreview/bin/Release/net48/UiPreview.exe outputs/qa
./tools/UiPreview/bin/Release/net48/UiPreview.exe --test
```

Dùng đúng project IFCInfo của repository này. Không cần CableTrayFromCad. Yêu cầu Windows, .NET Framework 4.8 và RevitAPI.dll 2024; không mở hay sửa model Revit. `--test` kiểm tra hộp thoại ở chế độ trong suốt rồi tự đóng. Thư mục `bin` của UiPreview có bản sao RevitAPI để chạy cục bộ; không đóng gói thư viện Autodesk này vào bản phát hành add-in.

## CadInspection: công cụ phụ CAD

- `dotnet build tools/CadInspection/CadInspection.csproj -c Release` cần AutoCAD 2023 tại đường dẫn tham chiếu trong csproj.
- Nạp DLL trong AutoCAD và chạy `DUMPTRAY` để đọc dynamic block của bản vẽ hiện hành; xuất `inspection.json` cạnh DLL.
- `Read-OpenCad.ps1` dùng Windows PowerShell 5.1 và AutoCAD COM đang mở để liệt kê bản vẽ.
- `read_dxf.py` cần Python 3, nhận ASCII DXF có sẵn và thư mục đầu ra rõ ràng:

```powershell
python tools/CadInspection/read_dxf.py 'path/to/sample.dxf' --output-dir work/cad-inspection
```

Script xuất `sample-outlines.tsv` và `parsed.json` trong thư mục được chỉ định. Không còn yêu cầu file mẫu hoặc thư mục test CableTray bên ngoài repository. Dùng để phân tích fixture, không phải bộ đọc DXF tổng quát hoặc chức năng của add-in IFC.
