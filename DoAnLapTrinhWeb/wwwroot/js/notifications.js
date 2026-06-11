(() => {
    if (document.body.dataset.authenticated !== "true") return;

    const count = document.getElementById("notificationCount");
    const list = document.getElementById("notificationList");
    const markRead = document.getElementById("markNotificationsRead");
    const dropdownButton = document.getElementById("notificationDropdownButton");
    const homeButton = document.getElementById("homeLatestNotificationButton");
    const homeCount = document.getElementById("homeLatestNotificationCount");
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

        if (items.length === 0) {
            list.innerHTML = '<div class="app-notification__empty">Chưa có thông báo dành cho tài khoản này.</div>';
            return;
        }

        list.innerHTML = items.map(item => `
            <a href="${escapeHtml(item.url || "#")}" class="app-notification__item ${item.isRead ? "" : "is-unread"}">
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

    markRead?.addEventListener("click", async event => {
        event.stopPropagation();
        if (!items.some(item => !item.isRead)) return;

        markRead.disabled = true;
        try {
            const response = await fetch("/Notification/MarkAllRead", { method: "POST" });
            if (!response.ok) throw new Error();
            items = items.map(item => ({ ...item, isRead: true }));
            render();
            markRead.classList.add("is-confirmed");
            markRead.innerHTML = '<i class="fas fa-check-double"></i> Đã đánh dấu';
            setTimeout(() => {
                markRead.classList.remove("is-confirmed");
                markRead.innerHTML = '<i class="fas fa-check"></i> Đã đọc';
            }, 1400);
        } catch {
            markRead.classList.add("is-error");
            setTimeout(() => markRead.classList.remove("is-error"), 1200);
        } finally {
            markRead.disabled = false;
        }
    });

    // Mở chuông chỉ để xem; số đỏ được giữ nguyên đến khi người dùng bấm "Đã đọc".
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
