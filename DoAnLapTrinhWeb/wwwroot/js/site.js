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
            const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
            window.location.href = `/Auth/Login?returnUrl=${returnUrl}`;
            return;
        }

        const data = await response.json();

        if (data.success) {
            updateFavoriteButtons(roomId, data.isFavorited);
            document.dispatchEvent(new CustomEvent('favoriteStatusChanged', {
                detail: { roomId, isFavorited: data.isFavorited }
            }));
        }
    } catch (error) {
        console.error("Lỗi:", error);
    }
}

function updateFavoriteButtons(roomId, isFavorited) {
    document.querySelectorAll(`[data-favorite-room-id="${roomId}"]`).forEach(button => {
        const icon = button.querySelector('i');
        if (!icon) return;

        icon.classList.toggle('fas', isFavorited);
        icon.classList.toggle('far', !isFavorited);
        icon.classList.toggle('text-danger', isFavorited);
        icon.classList.toggle('text-muted', !isFavorited);
        button.classList.toggle('is-favorited', isFavorited);
        button.setAttribute('aria-label', isFavorited ? 'Bỏ khỏi mục ưa thích' : 'Thêm vào mục ưa thích');
        button.setAttribute('title', isFavorited ? 'Bỏ khỏi mục ưa thích' : 'Thêm vào mục ưa thích');

        if (button.dataset.favoriteLabel === 'true') {
            const label = button.querySelector('span');
            if (label) label.textContent = isFavorited ? 'Đã lưu tin này' : 'Lưu tin này';
        }
    });
}

async function loadFavoriteStatuses() {
    if (document.body.dataset.authenticated !== 'true') return;
    if (!document.querySelector('[data-favorite-room-id]')) return;

    try {
        const response = await fetch('/Favorite/Status');
        if (!response.ok) return;
        const data = await response.json();
        const favoritedIds = new Set((data.accommodationIds || []).map(String));

        document.querySelectorAll('[data-favorite-room-id]').forEach(button => {
            updateFavoriteButtons(button.dataset.favoriteRoomId, favoritedIds.has(button.dataset.favoriteRoomId));
        });
    } catch (error) {
        console.error('Không thể tải trạng thái yêu thích:', error);
    }
}

document.addEventListener('DOMContentLoaded', loadFavoriteStatuses);


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

    window.setupAccumulatingFileInput = ({ inputId, previewId, maxFiles, allowedTypes, maxSize, errorMessage }) => {
        const input = document.getElementById(inputId);
        const preview = document.getElementById(previewId);
        if (!input || !preview) return;

        let selectedFiles = [];
        const fileKey = file => `${file.name}-${file.size}-${file.lastModified}`;

        const syncInputFiles = () => {
            const transfer = new DataTransfer();
            selectedFiles.forEach(file => transfer.items.add(file));
            input.files = transfer.files;
        };

        const renderPreview = (error = '') => {
            preview.innerHTML = '';
            selectedFiles.forEach((file, index) => {
                const item = document.createElement('div');
                item.className = 'listing-image-preview__item';

                if (file.type === 'application/pdf') {
                    item.innerHTML = '<div class="listing-document-preview__pdf"><i class="fas fa-file-pdf"></i><span>PDF</span></div>';
                } else {
                    const image = document.createElement('img');
                    image.alt = file.name;
                    image.src = URL.createObjectURL(file);
                    image.onload = () => URL.revokeObjectURL(image.src);
                    item.appendChild(image);
                }

                const removeButton = document.createElement('button');
                removeButton.type = 'button';
                removeButton.className = 'listing-image-preview__remove';
                removeButton.innerHTML = '<i class="fas fa-xmark"></i>';
                removeButton.setAttribute('aria-label', `Xóa ${file.name}`);
                removeButton.addEventListener('click', () => {
                    selectedFiles.splice(index, 1);
                    syncInputFiles();
                    renderPreview();
                });

                const name = document.createElement('span');
                name.textContent = file.name;
                item.append(removeButton, name);
                preview.appendChild(item);
            });

            if (error) {
                const errorElement = document.createElement('div');
                errorElement.className = 'listing-image-preview__error';
                errorElement.textContent = error;
                preview.appendChild(errorElement);
            }
        };

        input.addEventListener('change', function () {
            const newFiles = Array.from(this.files);
            const invalidFile = newFiles.find(file =>
                (allowedTypes && !allowedTypes.includes(file.type)) ||
                (maxSize && file.size > maxSize));

            if (invalidFile) {
                syncInputFiles();
                renderPreview(errorMessage);
                return;
            }

            const existingKeys = new Set(selectedFiles.map(fileKey));
            let exceededLimit = false;
            newFiles.forEach(file => {
                if (!existingKeys.has(fileKey(file))) {
                    if (selectedFiles.length < maxFiles) {
                        selectedFiles.push(file);
                        existingKeys.add(fileKey(file));
                    } else {
                        exceededLimit = true;
                    }
                }
            });

            syncInputFiles();
            renderPreview(exceededLimit ? errorMessage : '');
        });
    };

    window.setupAccumulatingFileInput({
        inputId: 'accommodationImageInput',
        previewId: 'accommodationImagePreview',
        maxFiles: 5,
        allowedTypes: ['image/jpeg', 'image/png', 'image/webp', 'image/gif'],
        errorMessage: 'Bạn chỉ được chọn tối đa 5 ảnh hợp lệ.'
    });

    window.setupAccumulatingFileInput({
        inputId: 'landDocumentInput',
        previewId: 'landDocumentPreview',
        maxFiles: 3,
        allowedTypes: ['image/jpeg', 'image/png', 'application/pdf'],
        maxSize: 10 * 1024 * 1024,
        errorMessage: 'Chỉ chọn tối đa 3 tệp JPG, PNG hoặc PDF; tối đa 10MB mỗi tệp.'
    });
});

// ==========================================
// 3. LẮNG NGHE SỰ KIỆN REAL-TIME: TRẠNG THÁI CHO THUÊ
// ==========================================
document.addEventListener('accommodationStatusChanged', function (e) {
    if (!e.detail) return;
    const accommodationId = e.detail.id;
    const isRentedOut = e.detail.isRentedOut;

    if (isRentedOut) {
        // Cập nhật nút Liên hệ ngay ở trang Details
        const contactBtn = document.getElementById('btnContactNow');
        if (contactBtn && contactBtn.getAttribute('onclick')?.includes(accommodationId)) {
            contactBtn.outerHTML = `<button type="button" class="room-booking-button is-disabled" style="background: #94a3b8; cursor: not-allowed;" disabled><i class="fas fa-ban"></i> Đã có người thuê / Hết phòng</button>`;
        }
        
        // Cập nhật các thẻ Card ở trang chủ (nếu có)
        // Tìm các thẻ có chứa data-id hoặc a href
        document.querySelectorAll('.room-card').forEach(card => {
            const link = card.querySelector(`a[href*="/Accommodation/Details/${accommodationId}"]`);
            if (link) {
                // Thêm badge Hết phòng
                const badgeContainer = card.querySelector('.room-card__badges') || card.querySelector('.room-card__image');
                if (badgeContainer && !badgeContainer.querySelector('.badge-rented')) {
                    const badge = document.createElement('span');
                    badge.className = 'badge bg-danger position-absolute top-0 end-0 m-2 badge-rented';
                    badge.textContent = 'Hết phòng / Đã thuê';
                    badgeContainer.appendChild(badge);
                }
            }
        });
    }
});
