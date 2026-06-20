(() => {
    if (document.body.dataset.authenticated !== "true") return;

    const count = document.getElementById("notificationCount");
    const list = document.getElementById("notificationList");
    const markRead = document.getElementById("markNotificationsRead");
    const dropdownButton = document.getElementById("notificationDropdownButton");
    const homeButton = document.getElementById("homeLatestNotificationButton");
    const homeCount = document.getElementById("homeLatestNotificationCount");
    const dashboardCount = document.getElementById("dashboardUnreadNotificationCount");
    let items = [];

    if (!list || !count) return;

    const escapeHtml = value => {
        const element = document.createElement("div");
        element.textContent = value || "";
        return element.innerHTML;
    };

    const iconFor = type => {
        if (type === "RentalRequest") return "fa-handshake";
        if (type === "RentalRequestSent") return "fa-paper-plane";
        if (type === "ReviewRequest") return "fa-star-half-stroke";
        if (type === "FavoriteUpdate") return "fa-heart-circle-bolt";
        if (type === "Approval") return "fa-circle-check";
        if (type === "Rejected") return "fa-circle-xmark";
        if (type === "PendingApproval") return "fa-list-check";
        return "fa-bell";
    };

    const render = () => {
        const unread = items.filter(item => !item.isRead).length;
        count.textContent = unread.toString();
        count.hidden = unread === 0;
        if (homeCount) {
            homeCount.textContent = unread.toString();
            homeCount.hidden = unread === 0;
        }
        if (dashboardCount) {
            dashboardCount.textContent = unread === 0
                ? "Tất cả thông báo đã đọc"
                : `${unread} thông báo chưa đọc`;
        }
        if (markRead) {
            markRead.disabled = unread === 0;
            if (unread === 0) markRead.innerHTML = '<i class="fas fa-check-double"></i> Đã đọc hết';
            else markRead.innerHTML = '<i class="fas fa-check"></i> Đánh dấu tất cả';
        }

        if (items.length === 0) {
            list.innerHTML = '<div class="app-notification__empty">Chưa có thông báo dành cho tài khoản này.</div>';
            return;
        }

        list.innerHTML = items.map(item => `
            <a href="${escapeHtml(item.url || "#")}" data-notification-id="${escapeHtml(item.id)}" class="app-notification__item ${item.isRead ? "" : "is-unread"}">
                <span class="app-notification__icon"><i class="fas ${iconFor(item.type)}"></i></span>
                <span>
                    <strong>${escapeHtml(item.title)}</strong>
                    <small>${escapeHtml(item.message)} · ${escapeHtml(item.createdAt)}</small>
                </span>
            </a>`).join("");
    };

    const addOrReplace = item => {
        if (!item?.id) return;
        const existingIndex = items.findIndex(current => String(current.id) === String(item.id));
        if (existingIndex >= 0) items.splice(existingIndex, 1);
        items.unshift(item);
        items = items.slice(0, 30);
        render();
        dropdownButton?.classList.remove("has-new");
        void dropdownButton?.offsetWidth;
        dropdownButton?.classList.add("has-new");
    };

    const markOneRead = async id => {
        const item = items.find(current => String(current.id) === String(id));
        if (!item || item.isRead) return true;

        item.isRead = true;
        render();
        document.querySelectorAll(`[data-notification-id="${CSS.escape(String(id))}"]`)
            .forEach(element => element.classList.remove("is-unread"));

        try {
            const response = await fetch(`/Notification/MarkRead?id=${encodeURIComponent(id)}`, {
                method: "POST",
                keepalive: true
            });
            if (!response.ok) throw new Error();
            return true;
        } catch {
            item.isRead = false;
            render();
            document.querySelectorAll(`[data-notification-id="${CSS.escape(String(id))}"]`)
                .forEach(element => element.classList.add("is-unread"));
            window.appToast?.("Không thể cập nhật trạng thái thông báo lúc này.", "error");
            return false;
        }
    };

    markRead?.addEventListener("click", async event => {
        event.stopPropagation();
        if (!items.some(item => !item.isRead)) return;

        markRead.disabled = true;
        try {
            const response = await fetch("/Notification/MarkAllRead", { method: "POST" });
            if (!response.ok) throw new Error();
            items = items.map(item => ({ ...item, isRead: true }));
            render();
            document.querySelectorAll("[data-notification-id].is-unread")
                .forEach(element => element.classList.remove("is-unread"));
            markRead.classList.add("is-confirmed");
            setTimeout(() => {
                markRead.classList.remove("is-confirmed");
            }, 1400);
        } catch {
            markRead.classList.add("is-error");
            setTimeout(() => markRead.classList.remove("is-error"), 1200);
        } finally {
            markRead.disabled = !items.some(item => !item.isRead);
        }
    });

    document.addEventListener("click", async event => {
        const link = event.target.closest("[data-notification-id]");
        if (!link) return;

        const url = link.getAttribute("href");
        const id = link.dataset.notificationId;
        if (!id) return;

        event.preventDefault();
        const marked = await markOneRead(id);
        if (marked && url && url !== "#") window.location.href = url;
    });

    // Mở chuông chỉ để xem; bấm từng thông báo sẽ đánh dấu riêng thông báo đó.
    homeButton?.addEventListener("click", () => dropdownButton?.click());
    document.addEventListener("notificationReceived", event => addOrReplace(event.detail));

    fetch("/Notification/Recent")
        .then(response => response.ok ? response.json() : [])
        .then(result => {
            items = result;
            render();
        })
        .catch(() => {
            list.innerHTML = '<div class="app-notification__empty">Không thể tải thông báo lúc này.</div>';
        });
})();
