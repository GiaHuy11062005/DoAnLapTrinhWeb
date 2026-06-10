using System.Collections.Generic;

namespace DoAnLapTrinhWeb.Models.ViewModels
{
    public class SearchStat
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalAccommodations { get; set; }
        public int PendingAccommodationsCount { get; set; }
        public int PendingLandlordsCount { get; set; }

        public IEnumerable<Accommodation> PendingRooms { get; set; } = new List<Accommodation>();
        public IEnumerable<LandlordApplication> PendingLandlordApplications { get; set; } = new List<LandlordApplication>();

        public List<SearchStat> TopSearchedCities { get; set; } = new List<SearchStat>();
        public List<SearchStat> AccommodationsByCity { get; set; } = new List<SearchStat>();
    }
}
