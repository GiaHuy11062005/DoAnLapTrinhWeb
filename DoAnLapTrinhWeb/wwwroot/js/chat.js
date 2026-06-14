// =========================================================
// CHAT.JS - Quản lý Mini Popup Chat kiểu Shopee
// =========================================================

(function () {
    "use strict";

    let chatConnection = null;
    let currentConversationId = 0;
    let myUserId = 0;

    const popupContainer = document.getElementById("globalChatPopupContainer");

    // =========================================================
    // MỞ POPUP (Global function)
    // =========================================================
    window.openChatPopup = async function (accommodationId, conversationId) {
        if (!popupContainer) {
            alert("Chat popup container không tồn tại trên trang này.");
            return;
        }

        try {
            // 1. Lấy conversationId nếu chỉ truyền accommodationId
            if (accommodationId && accommodationId > 0 && !conversationId) {
                const res = await fetch('/Chat/StartPopup', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: `accommodationId=${accommodationId}`
                });
                
                if (res.status === 401) {
                    window.location.href = "/Auth/Login?returnUrl=" + encodeURIComponent(window.location.pathname);
                    return;
                }
                
                const data = await res.json();
                if (!data.success) {
                    alert(data.message || "Không thể khởi tạo cuộc trò chuyện.");
                    return;
                }
                conversationId = data.conversationId;
            }

            if (!conversationId) return;

            // 2. Tải giao diện HTML
            const htmlRes = await fetch(`/Chat/GetChatHistory?conversationId=${conversationId}`);
            if (!htmlRes.ok) {
                if (htmlRes.status === 401) window.location.href = "/Auth/Login?returnUrl=" + encodeURIComponent(window.location.pathname);
                else alert("Lỗi tải cuộc trò chuyện.");
                return;
            }
            const html = await htmlRes.text();

            // 3. Render HTML
            popupContainer.innerHTML = html;
            popupContainer.classList.remove("is-hidden");
            popupContainer.classList.remove("is-minimized");

            // 4. Lấy dữ liệu cơ bản
            currentConversationId = conversationId;
            const innerDiv = document.getElementById("miniChatInner");
            if (innerDiv) {
                myUserId = parseInt(innerDiv.dataset.currentUserId) || 0;
            }

            scrollToBottom();
            initSignalR();
            markAsRead();

        } catch (error) {
            console.error("Lỗi khi mở popup:", error);
            alert("Lỗi khi kết nối đến cuộc trò chuyện.");
        }
    };

    // =========================================================
    // ĐÓNG / THU NHỎ POPUP
    // =========================================================
    window.closeChatPopup = function (e) {
        if (e) e.stopPropagation();
        popupContainer.classList.add("is-hidden");
        currentConversationId = 0;
    };

    window.toggleMinimizeChatPopup = function (e) {
        if (e) e.stopPropagation();
        // Không minimize nếu bấm nhầm vào các nút thao tác khác trên header
        if (e && e.target.closest('.mini-chat-btn-action') && !e.target.closest('.fa-minus')) {
            return;
        }
        popupContainer.classList.toggle("is-minimized");
    };

    // =========================================================
    // SỰ KIỆN DOM BẰNG DELEGATION (Vì HTML được render động)
    // =========================================================
    if (popupContainer) {
        popupContainer.addEventListener("click", function (e) {
            // Gửi tin nhắn
            if (e.target.closest("#chatSendBtn")) {
                sendMessage();
            }

            // Câu hỏi nhanh
            const quickBtn = e.target.closest(".chat-quick-btn");
            if (quickBtn) {
                const input = document.getElementById("chatInput");
                if (input) {
                    input.value = quickBtn.dataset.message || "";
                    input.focus();
                }
            }
        });

        popupContainer.addEventListener("keydown", function (e) {
            if (e.target.id === "chatInput" && e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });
    }

    // =========================================================
    // AJAX: GỬI TIN NHẮN
    // =========================================================
    async function sendMessage() {
        const input = document.getElementById("chatInput");
        const sendBtn = document.getElementById("chatSendBtn");
        if (!input || !sendBtn) return;

        const content = input.value.trim();
        if (!content || !currentConversationId) return;

        input.disabled = true;
        sendBtn.disabled = true;

        try {
            const res = await fetch("/Chat/SendMessage", {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: `conversationId=${encodeURIComponent(currentConversationId)}&content=${encodeURIComponent(content)}`
            });

            if (res.ok) {
                input.value = "";
            } else {
                const data = await res.json().catch(() => null);
                alert(data?.message || "Không thể gửi tin nhắn.");
            }
        } catch {
            alert("Lỗi kết nối khi gửi tin nhắn.");
        } finally {
            input.disabled = false;
            sendBtn.disabled = false;
            input.focus();
        }
    }

    // =========================================================
    // XÁC NHẬN / TỪ CHỐI THƯƠNG LƯỢNG
    // =========================================================
    window.confirmNegotiation = async function (conversationId) {
        if (!confirm("Bạn muốn xác nhận yêu cầu thương lượng này và đưa vào danh sách xử lý?")) return;
        try {
            const res = await fetch("/Chat/ConfirmNegotiation", {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: `conversationId=${encodeURIComponent(conversationId)}`
            });
            const data = await res.json();
            if (data.success) {
                const box = document.getElementById(`negotiationBox_${conversationId}`);
                if (box) box.remove();
            } else {
                alert(data.message || "Lỗi khi xác nhận.");
            }
        } catch {
            alert("Lỗi kết nối.");
        }
    };

    window.rejectNegotiation = async function (conversationId) {
        if (!confirm("Bạn muốn từ chối yêu cầu thương lượng này?")) return;
        try {
            const res = await fetch("/Chat/RejectNegotiation", {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: `conversationId=${encodeURIComponent(conversationId)}`
            });
            const data = await res.json();
            if (data.success) {
                const box = document.getElementById(`negotiationBox_${conversationId}`);
                if (box) box.remove();
            } else {
                alert(data.message || "Lỗi khi từ chối.");
            }
        } catch {
            alert("Lỗi kết nối.");
        }
    };

    // =========================================================
    // HELPER FUNCTIONS
    // =========================================================
    function escapeHtml(text) {
        const div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    function createBubble(senderName, content, time, isMine) {
        const wrapper = document.createElement("div");
        wrapper.className = `chat-bubble ${isMine ? "chat-bubble-mine" : "chat-bubble-other"}`;
        wrapper.innerHTML = `
            <div class="chat-bubble__name small">${escapeHtml(senderName)}</div>
            <div class="chat-bubble__content">${escapeHtml(content)}</div>
            <div class="chat-bubble__time small">${escapeHtml(time)}</div>
        `;
        return wrapper;
    }

    function scrollToBottom() {
        const msgs = document.getElementById("chatMessages");
        if (msgs) {
            msgs.scrollTop = msgs.scrollHeight;
        }
    }

    function markAsRead() {
        if (!currentConversationId) return;
        fetch("/Chat/MarkAsRead", {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: `conversationId=${encodeURIComponent(currentConversationId)}`
        }).catch(() => { });
    }

    // =========================================================
    // KẾT NỐI SIGNALR
    // =========================================================
    function initSignalR() {
        if (chatConnection) {
            // Đã kết nối, chỉ cần join group của conversation mới
            chatConnection.invoke("JoinConversation", currentConversationId).catch(() => {});
            return;
        }

        chatConnection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat")
            .withAutomaticReconnect()
            .build();

        chatConnection.on("MessageReceived", (data) => {
            // Nếu người dùng đang mở popup của conversation này
            if (data.conversationId === currentConversationId) {
                const isMine = data.senderId === myUserId;
                const bubble = createBubble(data.senderName, data.content, data.createdAt, isMine);
                
                const msgs = document.getElementById("chatMessages");
                if (msgs) {
                    const empty = msgs.querySelector(".mini-chat-empty");
                    if (empty) empty.remove();

                    msgs.appendChild(bubble);
                    scrollToBottom();
                }

                if (!isMine) {
                    markAsRead();
                }
            } else {
                // TODO: Xử lý hiển thị thông báo popup hoặc đỏ icon nếu chưa mở đúng box chat
                // Hiện tại hệ thống Notification chung (chuông góc trên) đã xử lý việc này
            }
        });

        chatConnection.on("NegotiationConfirmed", (conversationId) => {
            if (conversationId === currentConversationId) {
                const box = document.getElementById(`negotiationBox_${conversationId}`);
                if (box) box.remove();
            }
        });

        chatConnection.on("NegotiationRejected", (conversationId) => {
            if (conversationId === currentConversationId) {
                const box = document.getElementById(`negotiationBox_${conversationId}`);
                if (box) box.remove();
            }
        });

        chatConnection.start().then(() => {
            if (currentConversationId) {
                chatConnection.invoke("JoinConversation", currentConversationId);
            }
        }).catch(err => console.error("Lỗi SignalR Chat: ", err));
    }

})();
