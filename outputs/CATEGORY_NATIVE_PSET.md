# Category, phần tử native và IFC Pset — 11/09/2026

## Đã triển khai

1. Category nguồn không còn giới hạn Air Terminal. Sau khi chọn nguồn, nút
   **Đặt theo Category / Type** mở danh sách category model có type đã nạp hoặc có
   phần tử trong RVT chính. Có tìm tên Family/Type. Các category/type không có bộ
   dựng phù hợp vẫn hiện lý do và nút đặt bị khóa, không tạo Generic Model thay thế.
2. Các luồng tạo native:
   - Family đặt theo Level, host/mặt phẳng, hai Level, theo đường và adaptive.
   - Duct, Pipe, Cable Tray, Conduit theo đường tim và tiết diện. Chiều dài `0` giữ
     nguồn; số dương (mm) giữ đầu thứ nhất, thay đổi đầu còn lại. Duct/Pipe chọn
     System Type phù hợp. Không tự nối fitting trong luồng đặt chung này.
   - Basic Wall thẳng đứng có mặt bằng chữ nhật và bề dày khớp Wall Type.
   - Floor có biên phẳng ngang, gồm cả lỗ bên trong biên.
   - Roof phẳng ngang có một vòng biên, không có lỗ. Dùng Roof Type trong project.
3. Kết quả có **IFC Pset / Qto của dòng đang chọn**. Đọc property instance và type;
   hiển thị tên set, property, giá trị và đơn vị explicit nếu có. Có thuộc tính đơn,
   boolean, danh sách, enum, khoảng giá trị, complex property và quantity thông dụng.
   Giữ cả hai scope Instance/Type khi trùng tên. CSV có cột `IFC Psets`.
4. Pset được lưu kèm phần tử bằng Extensible Storage. Nếu không đọc được IFC gốc,
   thử các parameter mang tiền tố Pset/Qto trong link. Nếu không có, ghi rõ không có.
   Không tự tạo shared parameters trong bảng Properties của Revit.
5. Có tùy chọn giữ phần tử thành công khi một phần tử lỗi; mặc định hoàn tác cả lượt.
   Kiểm tra dấu nguồn chống tạo lại. Các tuyến còn được kiểm tra chồng đường tim
   với phần tử cùng category trước khi tạo. Duct tạo qua luồng chung vẫn lưu dấu
   nguồn/snapshot để dùng lại chức năng cập nhật IFC của luồng Duct.

## Phạm vi thực tế

Danh sách Category rộng hơn số bộ dựng native. **Chưa thể dựng tự động mọi hình học
của mọi category trong Revit.** Các category hệ thống ngoài bộ dựng trên (ví dụ
Stairs, Railing, Curtain Wall...) và family view-based/detail không được giả lập
bằng family điểm. Type không hỗ trợ có thông báo rõ trong UI.

- Family thường dùng kích thước của Family/Type đã chọn. Pset là thông tin nguồn,
  không tự ánh xạ mọi kích thước/tham số IFC sang family. Điểm nguồn có LocationPoint
  được ưu tiên; nếu không có, dùng tâm khung bao và góc xoay người dùng nhập.
- Family host dùng một mặt phẳng trong model chính; vị trí được chiếu lên mặt host.
  Adaptive cho chọn điểm điều khiển theo đúng thứ tự. Family theo đường dùng đường
  nguồn; nếu không đọc được thì yêu cầu chọn 2 điểm trong model. Esc hủy lượt.
- Tuyến IFC phải đọc được thành khối đùn thẳng tròn/chữ nhật (trực tiếp hoặc mapped),
  hoặc có LocationCurve/connector native. Chưa hỗ trợ flex, IFC BRep/mesh, profile
  U/I/hollow và đường cong trong bộ suy luận tuyến. Không suy đoán đường tim từ bbox.
- Kích thước pipe/conduit lấy theo tiết diện nguồn và đặt vào tham số đường kính;
  chưa tự chuyển đổi OD/ID/nominal theo catalog vật liệu.
- Wall hiện chưa dựng tường cong, nghiêng, có opening hoặc profile thay đổi.
  Floor/Roof chưa dựng hình học dốc, bậc, shape-edited. Roof chưa có lỗ.
- Wall/Floor/Roof được kiểm tra chênh lệch solid sau tạo; sai hình học/bề dày Type
  sẽ bị hoàn tác. Ngưỡng chênh thể tích 0,1% (có ngưỡng số học nhỏ).
- Pset có giá trị theo IFC gốc trên đĩa. Cần reload đúng phiên bản link. Giá trị
  chưa có đơn vị explicit được giữ nguyên; không tự suy đoán chuyển đổi đơn vị.

## Cách thử

1. Đóng Revit, thay DLL bằng `outputs/IFCInfo.dll`, mở lại Revit 2024.
2. Chọn IFC link → Category nguồn → chọn dòng → **Đặt theo Category / Type**.
3. Chọn Category đích và type đã nạp. UI chỉ hiện System Type/chiều dài/Level trên/
   góc xoay khi phù hợp với loại đã chọn.
4. Với Wall/Floor/Roof, chuẩn bị Type có bề dày khớp nguồn. Kiểm tra lần đầu trên
   một nguồn đơn giản. Family host/adaptive cần chọn mặt/điểm sau khi xác nhận.
5. Trong kết quả, chọn một dòng để xem IFC Pset/Qto phía dưới; xuất CSV khi cần.
   Luồng **Tạo Duct** chuyên biệt vẫn giữ các tùy chọn ánh xạ, cập nhật và fitting.

## Kết quả kiểm tra

- Build Release Revit 2024: **thành công, 0 lỗi / 0 cảnh báo**.
- 20 kiểm tra reader, 19 kiểm tra trùng, 25 kiểm tra snapshot/CSV/nối: đạt.
- 22 kiểm tra Pset mới: đạt (IFC2X3/IFC4, generic Wall, instance/type, Unicode,
  list, complex, null, quantity, property-set aggregate).
- 4 kiểm tra giao diện Category/Type: đạt; đã dựng và xem ảnh 3 cửa sổ WPF ngoài Revit.
- **Chưa chạy thao tác tạo native trên model Revit thực tế**. Các bộ dựng mới chưa
  được đánh dấu nghiệm thu thực tế. Mái dốc và các trường hợp ngoài phạm vi trên
  vẫn chưa hoàn thành, không được ghi “mọi category đã tạo thành công”.
