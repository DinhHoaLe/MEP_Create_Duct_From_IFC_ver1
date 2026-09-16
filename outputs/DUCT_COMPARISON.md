# Đối chiếu Duct

Cột đối chiếu so đường tim trong tọa độ model chính, kích thước với dung sai 1 mm, hướng tiết diện và System Type đích khi xác định được.

| Trạng thái | Ý nghĩa |
|---|---|
| Khớp hoàn toàn | Phủ đủ đường tim, đúng kích thước, hướng tiết diện và System Type đích. |
| Sai kích thước | Phủ đủ đường tim nhưng có duct khác kích thước/tiết diện. |
| Sai hệ thống | Phủ đủ đường tim nhưng có duct khác System Type đích. |
| Sai kích thước và hệ thống | Cả hai loại sai lệch cùng xuất hiện. |
| Sai góc tiết diện | Kích thước và hệ thống không bị báo sai nhưng hướng tiết diện chữ nhật khác nguồn. |
| Chưa xác định hệ thống | Phủ đủ, không phát hiện sai kích thước/hướng nhưng chưa xác định System Type đích. |
| Trùng một phần | Có đoạn chồng, chưa phủ đủ chiều dài; không kết luận kích thước/hệ thống toàn nguồn. |
| Chưa tồn tại | Không có đoạn đường tim chồng trong phạm vi thuật toán. |
| Chưa xác định | Không đọc được nguồn hoặc thiếu dữ liệu đường tim trong model chính để kết luận. |

Nếu đồng thời sai nhiều tiêu chí, bảng ưu tiên kích thước/hệ thống trước góc tiết diện. Tooltip cho biết ID, kích thước và System Type tương ứng.

Mọi trạng thái phủ đủ đều khóa chọn tạo mới để tránh chồng ống. Trùng một phần bị chặn ở bước chuẩn bị, không tự cắt phần thiếu. Trước tạo/cập nhật, tool kiểm tra chồng lại với các duct khác trong model.

System Type đích ưu tiên ánh xạ đã lưu trong Project Information; tiếp theo là dấu ánh xạ trên duct theo nguồn; nếu không có thì chỉ suy ra khi tên IFC khớp duy nhất tên type Revit, bỏ qua hoa/thường và khoảng trắng đầu/cuối. Ánh xạ được chọn trong cửa sổ tạo và chỉ lưu khi bật tùy chọn tương ứng.

“Khớp hoàn toàn” không xác nhận mạng nối kín, không so System Name IFC với tên mạng tự sinh của Revit. Nhiều đoạn có thể phủ một nguồn; nếu có duct chồng sai thì vẫn báo sai. Width/Height không tự hoán đổi; oval không coi là chữ nhật. Hướng trục tiết diện ngược dấu được coi tương đương khi đối chiếu.

Xem trước cập nhật IFC là luồng riêng, tìm duct theo dấu nguồn. Chỉ cập nhật duct có snapshot, chưa sửa tay và chưa nối mạng; không tự sửa mọi sai lệch được hiển thị trong bảng đối chiếu.

Kiểm tra logic: Test-DuctCoverage.ps1 (19), Test-DuctEnhancements.ps1 (25, gồm góc tiết diện và snapshot). Cần nghiệm thu Revit với link xoay/dịch, ống ghép phủ nguồn, sai kích thước/hệ thống/góc, partial overlap và nguồn chưa đọc được.
