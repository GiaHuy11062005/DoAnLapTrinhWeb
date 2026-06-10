// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ==========================================
// 1. HÀM DÙNG CHUNG CHO YÊU THÍCH PHÒNG TRỌ
// ==========================================
async function toggleGlobalFavorite(roomId, buttonElement) {
    try {
        const response = await fetch('/Favorite/ToggleFavorite', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `accommodationId=${roomId}`
        });

        // Nếu chưa đăng nhập, chuyển hướng sang trang Login
        if (response.status === 401) {
            window.location.href = "/Auth/Login";
            return { success: false, unauthorized: true };
        }

        const data = await response.json();

        if (data.success) {
            // Đổi giao diện icon trái tim tùy theo trạng thái
            const icon = buttonElement.querySelector('i');
            if (data.isFavorited) {
                icon.classList.remove('far', 'text-muted'); // Bỏ icon rỗng
                icon.classList.add('fas', 'text-danger');   // Thêm icon đặc màu đỏ
            } else {
                icon.classList.remove('fas', 'text-danger');
                icon.classList.add('far', 'text-muted');
            }
        }

        return data;
    } catch (error) {
        console.error("Lỗi:", error);
        return { success: false, error: true };
    }
}

// Alias để những view cũ vẫn chạy được
window.toggleGlobalFavorite = toggleGlobalFavorite;
window.toggleFavorite = async function (roomId, buttonElement) {
    return toggleGlobalFavorite(roomId, buttonElement);
};


// ==========================================
// 2. XỬ LÝ AJAX ĐỔ DỮ LIỆU ĐỊA CHỈ (POPUP ĐĂNG BÀI)
// ==========================================
$(document).ready(function () {
    // 2.1. Gọi API lấy danh sách Tỉnh/Thành ngay khi trang web vừa load xong
    var modalCitySelect = $('#modalCitySelect');
    if (modalCitySelect.length > 0) {
        $.ajax({
            url: '/Accommodation/GetAllCities',
            type: 'GET',
            success: function (data) {
                modalCitySelect.empty();
                modalCitySelect.append('<option value="" selected disabled>-- Chọn Tỉnh/Thành phố --</option>');

                // Vòng lặp đổ dữ liệu vào ô select
                $.each(data, function (index, item) {
                    modalCitySelect.append('<option value="' + item.id + '">' + item.name + '</option>');
                });
            },
            error: function () {
                modalCitySelect.empty();
                modalCitySelect.append('<option value="" selected disabled>Lỗi khi tải dữ liệu</option>');
            }
        });
    }

    // 2.2. Xử lý sự kiện khi Chủ trọ chọn một Tỉnh/Thành -> Load Quận/Huyện tương ứng
    $('#modalCitySelect').change(function () {
        var cityId = $(this).val();
        var districtSelect = $('#modalDistrictSelect');

        // Khóa ô Quận/Huyện và hiện chữ Đang tải trong lúc chờ dữ liệu
        districtSelect.empty();
        districtSelect.append('<option value="" selected disabled>-- Đang tải... --</option>');
        districtSelect.prop('disabled', true);

        if (cityId) {
            $.ajax({
                url: '/Accommodation/GetAllDistricts',
                type: 'GET',
                data: { cityId: cityId },
                success: function (data) {
                    districtSelect.empty();
                    districtSelect.append('<option value="" selected disabled>-- Chọn Quận/Huyện --</option>');

                    $.each(data, function (index, item) {
                        districtSelect.append('<option value="' + item.id + '">' + item.name + '</option>');
                    });

                    // Mở khóa ô Quận/Huyện cho người dùng chọn
                    districtSelect.prop('disabled', false);
                },
                error: function () {
                    districtSelect.empty();
                    districtSelect.append('<option value="" selected disabled>Lỗi khi tải dữ liệu</option>');
                }
            });
        }
    });
});
