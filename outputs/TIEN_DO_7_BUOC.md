# Tiến độ 7 bước — 10/09/2026

**Đã triển khai code cho cả 7 mục.** Build Revit 2024 Release thành công, 0 lỗi / 0 cảnh báo.
64 kiểm tra tự động đạt; đã dựng và xem ảnh hai cửa sổ WPF ngoài Revit.

“Thành công” dưới đây chỉ áp dụng cho kiểm tra đã thực sự chạy. Chưa chạy các
thao tác model trong Revit, vì vậy chưa đánh dấu nghiệm thu thực tế cho toàn bộ 7 bước.

| Bước | Phần đã làm | Kết quả đã xác nhận | Thử trong Revit |
|---|---|---|---|
| 1. Kiểm tra trùng | Đường tim, kích thước, System Type theo ánh xạ đã lưu và hướng tiết diện; Khớp hoàn toàn / Sai kích thước / Sai hệ thống / Trùng một phần / Sai góc tiết diện. Kiểm tra chồng lại ngay trước tạo/cập nhật. | **Thành công: kiểm tra logic tự động** | Chờ thử với duct native, hệ thống và link thực |
| 2. Tìm trong model | Bấm dòng → Zoom tới nguồn IFC / Chọn duct tương ứng / Cô lập trong view 3D mới. | Đã triển khai, build thành công | Chờ xác nhận chọn linked element, zoom và view 3D |
| 3. Lưu ánh xạ | Duct Type theo tiết diện; System Type và Workset theo nhóm hệ thống/tiết diện; Level theo tên Level nguồn. Lưu trong Project Information của RVT. | Đã triển khai, cửa sổ thiết lập dựng thành công | Chờ tạo, lưu RVT, đóng/mở lại và thử model workshared |
| 4. Báo cáo | Mỗi nguồn → ID đích → trạng thái/lý do; CSV UTF-8; báo cáo cả hoàn tác và lỗi fitting; chọn lại duct trong lượt hiện tại hoặc lượt tạo đã lưu. Có xuất CSV lỗi ở bước chuẩn bị. | **Thành công: kiểm tra CSV tiếng Việt, dấu phẩy, ngoặc kép, nhiều dòng, chống công thức; cửa sổ kết quả dựng thành công** | Chờ đối chiếu ID và chọn lại trong model |
| 5. Xử lý lỗi | Transaction riêng cho từng đoạn/kết nối, chung một TransactionGroup; mặc định hoàn tác toàn bộ, tùy chọn giữ phần thành công. Báo cả những dòng chưa thực hiện. | Đã triển khai, build thành công | Chờ gây lỗi tạo/fitting và kiểm tra rollback/Undo |
| 6. Cập nhật IFC | Lưu hình học nguồn và trạng thái duct sau tạo/nối; xem trước vị trí, W/H/D và hệ thống trước/sau; chọn từng dòng cập nhật. Kiểm tra lại sửa tay khi thực hiện. | **Thành công: kiểm tra so trạng thái và ngưỡng sai số** | Chờ reload IFC và cập nhật trên model thực |
| 7. Fitting / connector | Elbow, transition, tee; nối đầu cùng tiết diện; nối miệng gió native. Khoảng hở elbow/transition tùy chọn 1–1000 mm, chỉ cặp có phương án duy nhất; dùng Routing Preferences. | **Thành công: kiểm tra hình học ứng viên nối; code API build thành công** | Chờ thử family/routing thực tế, tee, elbow, transition và miệng gió |

## Cách dùng

1. Đóng Revit. Thay DLL cài đặt bằng `outputs/IFCInfo.dll`, rồi mở lại Revit 2024.
2. Chọn link và category Duct. Cột đối chiếu có tooltip giải thích và các ID duct trùng.
3. Bấm một ô của dòng rồi dùng nút tìm/zoom/cô lập. Hộp thoại đóng để thao tác trong model;
   chạy lại tool để tiếp tục. View cô lập là view mới, có section box quanh nguồn,
   isolate tạm link và các duct tương ứng; không thay đổi view làm việc cũ.
4. Chọn các nguồn cần tạo → Tạo Duct. Cuộn xuống để chọn Level, Workset, chính sách lỗi,
   tùy chọn nối, khoảng hở fitting và kiểm tra bảng xem trước. Có thể bỏ chọn từng dòng.
5. Lưu ánh xạ được bật mặc định. Cần Save RVT để giữ cấu hình qua phiên làm việc.
6. Sau khi tạo, xuất CSV nếu muốn giữ báo cáo chi tiết. Nút chọn toàn bộ duct của lượt
   đóng hộp thoại và chọn các duct đã thành công. Khi chạy lại, danh sách lượt đã lưu
   cho phép chọn các duct còn tồn tại của lượt tạo đó; cập nhật giữ nguyên nhóm lượt tạo.
7. Với phiên bản IFC mới, **reload cùng link** rồi bấm **Xem trước cập nhật IFC**.
   So sánh bảng trước/sau, kiểm tra ánh xạ đích và chỉ chọn các dòng muốn cập nhật.

## Phạm vi và giới hạn cần biết khi nghiệm thu

- “Khớp hoàn toàn” là phủ đường tim nguồn, đúng kích thước/hướng tiết diện và System Type
  đích. Không đồng nghĩa mạng đã nối kín; tên hệ thống tự sinh trong Revit không dùng
  thay cho System Name IFC. Nhiều đoạn liên tiếp có thể cùng phủ một nguồn.
- Nguồn trùng một phần bị chặn khi chuẩn bị; không tự cắt bỏ phần chồng hoặc tạo chồng ống.
- Tìm duct tương ứng dựa trên kết quả hình học; một duct đã di chuyển khỏi nguồn có thể
  không xuất hiện trong nút này, nhưng vẫn được tìm theo dấu nguồn ở bước cập nhật.
- Revit không isolate trực tiếp một phần tử bên trong link bằng API isolate host. View
  kiểm tra isolate link và cắt vùng quanh nguồn; phần tử IFC lân cận trong vùng có thể còn hiện.
- Level lấy từ LevelId của phần tử link; nếu không có, dùng nhóm “Không có Level nguồn”.
  Chế độ tự động chọn Level dưới cao độ đầu thấp nhất. Workset chỉ hiện ở model workshared.
  Type/Level đã xóa không tự được chọn lại theo ID cũ; cần chọn một giá trị hiện có.
- Cập nhật dựa trên link UniqueId + IFC GUID. Chỉ tự cập nhật duct có snapshot của bản này,
  chưa sửa tay, chưa ghim và chưa kết nối. Duct của bản cũ không có snapshot sẽ được báo
  bỏ qua. Không tự nhận nguồn đổi GUID/xóa nguồn hoặc thay link bằng instance mới.
- Theo dõi sửa tay gồm đường tim, hướng/kích thước, Duct Type, System Type, Level, Workset,
  Comments, trạng thái ghim và các kết nối. Không theo dõi mọi tham số tùy biến/insulation.
- Fitting **mặc định tắt**. Tee cần ba đầu ống gặp nhau trong 1 mm; chưa chia một duct ở
  giữa để đặt tee. Elbow/transition qua khoảng hở chỉ được thử khi tăng giới hạn; Revit
  có thể trim/extend đầu duct. Không tự dựng lại hình học fitting từ IFC.
- Chỉ nối giữa duct thành công trong lượt và, nếu bật, miệng gió native của model chính.
  Không nối connector bên trong IFC link hoặc tự sửa mạng duct cũ. Cặp duct phải cùng
  System Type và System Name nguồn. Miệng gió phải khớp tiết diện, hướng, phân loại HVAC.
- Thiếu family/routing, nút nối mơ hồ hoặc Revit từ chối fitting được báo rõ. Mặc định
  lỗi fitting hoàn tác cả lượt; chọn “Bỏ qua lỗi, giữ phần thành công” nếu muốn giữ duct.
- Nếu lưu trạng thái cuối sau fitting thất bại trong chế độ giữ thành công, báo cáo sẽ
  ghi lỗi; lần cập nhật sau có thể cảnh báo duct đã đổi, thay vì tự ghi đè.

## Checklist nghiệm thu trong Revit — chưa thực hiện

- [ ] 1: Mẫu đúng; đổi width; đổi System Type; xoay tiết diện; phủ nửa đường tim.
- [ ] 2: Link có xoay/dịch chuyển; zoom nguồn; chọn nhiều duct phủ nguồn; view 3D cắt đúng vùng.
- [ ] 3: Lưu/đóng/mở RVT; ánh xạ còn đúng; thử Workset và một Level đã bị xóa.
- [ ] 4: ID trong báo cáo tồn tại; CSV giống bảng; chọn lại lượt sau khi mở lại tool.
- [ ] 5: Một đoạn lỗi giữa lượt; so cả hai chế độ; lỗi fitting; Undo khôi phục toàn lượt.
- [ ] 6: Tạo bằng bản mới, sửa IFC và reload; xác nhận trước/sau; cập nhật giữ ID nếu Revit cho phép.
- [ ] 6: Sửa duct bằng tay trước update; xác nhận tool cảnh báo và không ghi đè.
- [ ] 7: Elbow/transition/tee với routing hợp lệ; family thiếu; khoảng hở quá giới hạn; nút mơ hồ; miệng gió.

## Kiểm tra đã chạy

- `Test-IfcReader.ps1`: 20 assertions; file CADMEP có 6 duct thẳng đọc đúng kích thước.
- `Test-DuctCoverage.ps1`: 19 checks.
- `Test-DuctEnhancements.ps1`: 25 checks (snapshot, góc tiết diện, CSV, ứng viên nối).
- `Render-DuctWindows.ps1`: dựng cửa sổ thiết lập/kết quả và xuất ảnh offscreen, đã xem ảnh.
- `dotnet build outputs/IFCInfo.csproj -c Release --no-restore`: 0 lỗi, 0 cảnh báo.

Chưa cài DLL vào thư mục add-in, chưa chạy sửa model Revit, chưa commit/merge.
