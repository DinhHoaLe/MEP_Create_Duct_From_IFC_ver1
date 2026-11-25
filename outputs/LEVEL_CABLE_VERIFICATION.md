## Service Type selector và Pipe slope — 25/11/2026

- Cable Tray Manual: danh sách giá trị Service Type đã fill từ host Cable Tray và IFC được trim, loại trùng, sắp xếp; không có Follow IFC/Manual value trong danh sách. Chọn Enter new… mới hiện ô nhập bên dưới. Ô mới rỗng chặn Create. Khi không có source/service, không chọn ngầm giá trị đầu tiên; cho chọn Keep native default rõ ràng.
- Auto: shared selector cho Follow IFC, giá trị đã có hoặc Enter new…. Trong từng card hiển thị selector kết quả bị khóa cho Service Type, Type và Level. Trở về Manual khôi phục service đã chọn/nhập, không đổi thành Follow IFC. Sidebar service selector ẩn khi Manual.
- Pipe Elements: thêm Slope (%) chỉ ở category Pipes, sắp xếp theo SlopePercent. Giá trị = abs(dZ) / sqrt(dX²+dY²) × 100, lấy từ centerline đã qua transform link. Ống đứng = Vertical, không đọc được/đường tâm suy biến = Unavailable. Không đổi hình học hay giá trị slope trong model. Copy/export bảng Pipe có thêm slope.
- Build Release Revit2023/net48 x64: 0 warnings/errors. **91 checks PASS**, không mở cửa sổ; bao gồm trạng thái selector, existing/new/default, khôi phục manual, new value rỗng, slope 0/1/2%, đảo đầu mút, vertical/degenerate/NaN, cột chỉ xuất hiện cho Pipe. Render 4 PNG tại outputs/verification/service-slope; đã xem Manual existing, Auto IFC và bảng Pipe slope, cùng kiểm tra ảnh sau khi thống nhất Auto Type.
- MCP run **996fac789d22**, pid17576 / Revit2023 / new.rvt, read-only: 10 linked Pipes đọc được slope: 288221/288266/288330 = 0%; 288336/288893/288900/288907/288914 ≈ 4.167%; 288920/288926 = Vertical. Host Cable Tray Service Type hiện không có giá trị đã fill. Không dựng dữ liệu giả vào catalog thật.
- Get_run: 0 added, 0 modified, 0 deleted; transactions/dialogs rỗng. Document IsModified=true đã có trước query và vẫn true sau; không save, không Create/export native, không điều khiển chuột/bàn phím.
- DLL release được copy từ build và đối chiếu SHA-256. Command IFCInfo.IFCInfoCommand, Add-In Manager → Manual. Lần này chưa chạy command tương tác trong Revit hoặc xác nhận ghi service khi Create trên model thật.

---

# Bổ sung giao diện đồng bộ — 25/11/2026

- Level nằm trong **từng source group card** của Duct, Pipe, Cable Tray và Conduit, ngay sau Type. Không còn mapping level rời dưới danh sách card hoặc chỉ nằm ở sidebar của Duct.
- Các field Level dùng cùng bố cục icon 50 px, cột icon 70 px, title/hint và selector 46 px như System Type/Type; icon Level riêng. Khoảng cách trong card thống nhất. Auto vẫn hiển thị selector Level bị khóa với giá trị Follow IFC đã resolve. Manual có Follow IFC hoặc level cụ thể.
- Cable Tray Service Type dùng cùng full-size field/icon; giữ Follow IFC / Manual value, hiện giải thích giá trị nguồn hoặc thiếu nguồn. Ô nhập Manual/shared được bo góc và dùng cùng màu/kích thước của selector.
- Duct có nhiều shape/system cards cùng source level: các selector cùng level key được đồng bộ, giữ schema mapping hiện có. Đổi Auto/Manual giữ lựa chọn riêng, gửi đúng level mapping trong request.
- Build Release 2023/net48 x64: **0 warnings / 0 errors**. `UiPreview.exe --test-curves`: **72 PASS**, gồm 18 kiểm tra bố cục mới và 54 kiểm tra trước. Không gọi Show/ShowDialog trong chế độ này.
- `UiPreview.exe --render-settings <folder>`: **18 layout checks PASS**, tạo 8 PNG cho Duct/Pipe/CableTray/Conduit × Auto/Manual, không mở cửa sổ. Đã xem ảnh Duct Auto, Pipe Manual, CableTray Manual và kiểm tra trực quan Level/Service alignment.
- Ảnh: `outputs/verification/ui-level-cable/`. DLL release được copy từ build và kiểm tra SHA-256 trùng khớp.
- Lượt bổ sung UI không chạy Create/export native hoặc sửa model. Các kết quả MCP geometry ở phần lịch sử bên dưới vẫn là kiểm tra chỉ đọc trước đó.

---

# Level và Cable Tray — 25/11/2026

Project: MEP_Create_Duct_From_IFC_ver1. Command: IFCInfo.IFCInfoCommand.

## Kết quả

- Release Revit 2023 / net48 x64 build: 0 warnings, 0 errors. Dùng DLL API cài trong Revit 2023, không copy API vào gói release.
- UiPreview.exe --test-curves: **54 checks PASS**, không gọi Show/ShowDialog. Kiểm tra Follow IFC ở Auto/Manual cho Pipe, Cable Tray, Conduit; mapping level riêng theo source; đổi Auto/Manual giữ manual choices; missing/duplicate levels bị chặn; đối chiếu family/type chính xác; Service Follow IFC trong Manual; sai profile bị chặn trước tạo.
- Test-IfcCableLevels.ps1 -SampleIfc .../1122_IFC_MEPF.ifc: **8 checks PASS**. Bao gồm heightwise extrusion, source type/storey, generic duct ambiguity, vertical extrusion không bị đổi trục và ba cable tray mẫu.
- Test-IfcReader.ps1: **27 assertions PASS**. Test-IfcReaderEdgeCases.ps1 PASS. Test-IfcProperties.ps1: **22 checks PASS**.

## MCP thực tế, chỉ đọc

Revit 2023 / 23.0.11.19, document new.rvt, link 1122_IFC_MEPF.ifc.RVT. Không điều khiển chuột/bàn phím, không tạo Transaction, không tạo/sửa/xóa/lưu model.

Run f6b796627f2f: đọc geometry của DLL đã sửa trên linked elements:

| Linked ID | Length mm | Width mm | Height mm | Centerline đọc được |
|---|---:|---:|---:|---|
| 288352 | 12801.6 | 304.8 | 101.6 | Có |
| 288404 | 4876.8 | 304.8 | 101.6 | Có |
| 288407 | 5334.0 | 304.8 | 101.6 | Có |

Get_run xác nhận 0 added / 0 modified / 0 deleted, transactions rỗng, không dialog. Document IsModified=false sau query.

Run 00200f907e16 với DLL cuối: ba đoạn trên đều resolve type **Cable Tray with Fittings : Default**, level **Level 1**, source issue rỗng. Hai ID 288934, 288937 là Conduit tròn nhưng importer đặt dưới category Cable Trays: báo lỗi profile/category rõ ràng, không tự đổi category hoặc tạo thành tray. Document IsModified=false sau query.

Hai lần chuẩn bị query không chạy được: một đường dẫn TEMP khác giữa process, một lần ambiguous_revit khi người dùng mở thêm phiên Revit. Đã sửa đường dẫn và chỉ rõ pid 17576; các run thành công ở trên là bằng chứng cuối.

## Quy tắc level

Follow IFC dùng tên level nguồn duy nhất, sau đó cao độ source level đã chuyển sang internal coordinates của host. Nếu thiếu source level thì chọn floor tại/dưới tâm hình học nguồn trong host coordinates. Missing/ambiguous match phải chọn thủ công; không mặc định level đầu, không tạo level mới. Tọa độ endpoints từ IFC link được giữ nguyên khi chọn reference level.

Auto: Follow IFC hoặc shared project level. Manual: mỗi nhóm source level có Follow IFC hoặc project level riêng. Review hiển thị target level từng phần tử; request mang SourceLevelIds và backend sử dụng từng level ID.

## Bước người dùng còn cần thử

Load release/IFCInfo-Revit2023/IFCInfo_Revit2023.dll bằng Add-In Manager → Manual, command IFCInfo.IFCInfoCommand. Nếu phiên đã giữ DLL cũ, đóng/mở lại Revit trước khi load.

Chọn ba tray chữ nhật; bỏ chọn hai conduit thuộc nhầm category Cable Trays. Kiểm tra Auto/Manual → Follow IFC → Review. **Chưa nhấn Create/export native trong model thật** do yêu cầu kiểm thử chỉ đọc. Việc tạo native, kiểm tra reference level/offset/roll sau tạo và xử lý giao dịch chỉ được xác nhận khi người dùng tự thử trên bản model kiểm thử phù hợp. Không xác nhận toàn bộ native export chỉ từ build và geometry read.
