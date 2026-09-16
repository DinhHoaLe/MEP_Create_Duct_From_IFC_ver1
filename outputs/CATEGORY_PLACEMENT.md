# Category nguồn quyết định Category đích

Luồng đặt native chỉ hiển thị Family/Type có cùng Category ID với Category nguồn trong IFC link. Ví dụ Duct Fittings → Duct Fittings, Air Terminals → Air Terminals, Walls → Walls. Tên hiển thị có thể khác theo ngôn ngữ Revit; đối chiếu dùng ID.

Category đích được cố định. Nếu chưa có type, nạp family `.rfa` đúng Category ngay trong hộp thoại. Family khác Category sẽ bị hoàn tác việc nạp. Duct, Pipe, Wall và các system family vẫn dùng type native tương ứng; không phải tất cả Category đều nạp được từ `.rfa`.

Trước khi đặt, tool kiểm tra Category của type đích và từng phần tử nguồn. Category chưa hỗ trợ hoặc chưa có type không được chuyển sang Category khác để tạo. Flex Ducts không dùng luồng tạo Duct thẳng.

Quy tắc này dùng Category của phần tử trong Revit IFC link; không suy đoán Category từ tên phần tử hoặc tự phân loại lại IFC bị nhập thành Generic Models. Đây là ràng buộc Category, không tự chọn family chính xác theo hình dạng/kích thước nguồn.

Kiểm tra: build Release và script `outputs/tests/Render-DuctWindows.ps1`. Nạp family và tạo phần tử thực tế vẫn cần kiểm chứng trong Revit.
