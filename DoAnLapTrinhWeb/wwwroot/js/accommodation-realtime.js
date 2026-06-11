(() => {
    if (!window.signalR || document.body.dataset.authenticated !== "true") {
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/accommodations")
        .withAutomaticReconnect()
        .build();

    const forward = (eventName, detail) => {
        document.dispatchEvent(new CustomEvent(eventName, { detail }));
    };

    connection.on("PendingAccommodationCreated", detail => forward("pendingAccommodationCreated", detail));
    connection.on("PendingAccommodationProcessed", detail => forward("pendingAccommodationProcessed", detail));
    connection.on("AccommodationStatusChanged", detail => forward("accommodationStatusChanged", detail));
    connection.on("NotificationReceived", detail => forward("notificationReceived", detail));

    connection.start().catch(error => console.error("Không thể kết nối cập nhật trạng thái:", error));
})();
