using System.Collections.Generic;

namespace DoAnLapTrinhWeb.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public IEnumerable<Accommodation> PendingRooms { get; set; } = new List<Accommodation>();
        public IEnumerable<LandlordApplication> PendingLandlordApplications { get; set; } = new List<LandlordApplication>();
    }
}
