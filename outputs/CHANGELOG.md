# Bản sửa 16/09/2026

## Thay đổi

- Chỉ kiểm tra Duct Type/System Type của các dòng được chọn trong bảng xem trước. Request và cấu hình lưu chỉ chứa ánh xạ hệ thống, Level, Workset của nhóm được chọn.
- Khóa Delete trong bảng xem trước. Checkbox Thực hiện giữ các dòng bỏ chọn trong báo cáo; số lượng và trạng thái nút cập nhật theo lựa chọn.
- Nhớ đường dẫn IFC chọn thủ công trên LinkOption trong phiên hộp thoại; đọc lại Category hoặc quay lại cùng link không mất đường dẫn.
- UiPreview chuyển sang đúng giao diện IFC/Duct, tham chiếu IFCInfo.csproj; không phụ thuộc CableTrayFromCad. Có chế độ --test kiểm tra WPF tạo và cập nhật.
- Đồng bộ README, hướng dẫn cài/sử dụng, đối chiếu Duct và ghi rõ tài liệu lịch sử. Giữ checklist nghiệm thu Revit chưa hoàn tất.
- Công cụ read_dxf.py nhận đường dẫn đầu vào/thư mục đầu ra từ dòng lệnh; không phụ thuộc file mẫu hay thư mục CableTray bên ngoài. Kiểm tra cả cặp tọa độ X/Y trước khi đọc khoảng cách.

## Kiểm tra

- IFCInfo, UiPreview và CadInspection: build Release thành công, 0 lỗi / 0 cảnh báo.
- Test-IfcReader: 13 assertions; Test-DuctCoverage: 19; Test-DuctEnhancements: 25.
- Test-DuctUi: 26 kiểm tra WPF mới, bao gồm nhóm chưa ánh xạ vẫn được chọn phải bị chặn, bỏ chọn nhóm thiếu type/ánh xạ được tiếp tục, chỉ lưu nhóm được chọn, Delete không xóa nguồn, dòng bỏ chọn được báo cáo và số lượng cập nhật.
- Tổng: 83 kiểm tra C#/WPF đạt. File CADMEP bên ngoài không có trong repository nên chưa chạy lại 7 assertions tùy chọn.
- read_dxf.py: 6 kiểm tra CLI với fixture DXF tổng hợp đạt (đường dẫn đầu vào/đầu ra, kích thước, JSON/TSV, file thiếu và --help). Chưa thử bản vẽ CAD thực.
- Dựng và xem ảnh cửa sổ thiết lập/kết quả; thử kích thước thiết lập thường và thu gọn.

## Giới hạn

Chưa chạy thao tác model thực trong Revit hoặc AutoCAD: tạo/cập nhật duct, routing/fitting, host, worksharing, lưu RVT, rollback và Undo vẫn cần nghiệm thu trên mô hình thử. Chưa đo hiệu năng mô hình lớn. Bản sửa không tự cài DLL vào thư mục add-in của Revit.

