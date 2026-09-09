# Đối chiếu Duct — bước 1

Cột **Đối chiếu Duct** so đường tim trong tọa độ model chính và kích thước với dung sai 1 mm.

| Trạng thái | Ý nghĩa |
|---|---|
| Khớp hoàn toàn | Đường tim được phủ đủ; mọi duct chồng trên đường tim có cùng tiết diện, kích thước và System Type đích. |
| Sai kích thước | Đường tim được phủ đủ nhưng có duct khác tiết diện hoặc kích thước. |
| Sai hệ thống | Đường tim được phủ đủ nhưng có duct khác System Type đích. |
| Sai kích thước và hệ thống | Phát hiện cả hai loại sai lệch. |
| Chưa xác định hệ thống | Đường tim và kích thước khớp nhưng chưa xác định được System Type đích. |
| Trùng một phần | Có phần đường tim trùng nhưng chưa phủ đủ chiều dài. Chưa kết luận kích thước/hệ thống cho cả đoạn. |
| Chưa tồn tại | Không tìm thấy đoạn đường tim trùng. |
| Chưa xác định | Không đọc được nguồn, hoặc dữ liệu đường tim trong model chính chưa đầy đủ để kết luận không tồn tại/phủ một phần. |

Di chuột lên trạng thái để xem ID, kích thước nguồn/đích và System Type ID. Mọi trạng thái phủ đủ đường tim đều không cho chọn tạo thêm; tool chưa sửa duct sai lệch. Đoạn trùng một phần vẫn theo luồng chọn tạo hiện có, chưa tự cắt lấy phần thiếu.

System Type đích được lấy từ ánh xạ lưu trên duct do phiên bản này tạo (theo link instance, nguồn và System Type nguồn). Nếu chưa có dữ liệu lưu, chỉ suy ra khi tên System Type IFC khớp duy nhất tên type trong Revit, bỏ qua hoa/thường và khoảng trắng đầu/cuối. Ánh xạ cũ khác tên không được đoán là sai. Tool chưa cung cấp giao diện nhập ánh xạ cho phần đối chiếu.

“Khớp hoàn toàn” chỉ trong phạm vi đường tim, tiết diện, kích thước và System Type. Chưa kiểm tra góc xoay tiết diện, System Name hay kết nối connector. Width/Height so theo tên, không tự hoán đổi. Nếu nhiều duct chồng nhau, bất kỳ duct sai nào cũng được báo, kể cả khi một duct khác khớp. Tiết diện oval không coi là chữ nhật cùng Width/Height.

Kiểm tra tự động: `pwsh -NoProfile -File outputs/tests/Test-DuctCoverage.ps1` (19 trường hợp, không cần Revit).

Kiểm tra cần thực hiện trong Revit 2024: tạo bằng ánh xạ khác tên rồi chạy lại; sửa kích thước/System Type; kiểm tra link xoay/dịch chuyển; kiểm tra nhiều đoạn phủ đủ và trùng một phần. Kiểm tra tooltip, màu trạng thái và checkbox bị khóa cho đoạn đã phủ đủ. Chưa xác minh các thao tác này trong phiên Revit thực tế.
