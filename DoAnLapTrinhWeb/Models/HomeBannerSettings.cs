using System.Collections.Generic;

namespace DoAnLapTrinhWeb.Models
{
    public class HomeBannerSettings
    {
        public string ImageUrl { get; set; } =
            "https://lh3.googleusercontent.com/aida-public/AB6AXuAAaYSYGxCGorZYvZQ9BWjP3sFH6QGK16s-NLFFMdQEHBoCDJqLQHbYLwuI_6GRWxcUqLDAlY3-3h_Kg9xSuBNax8a12ycbw2gXJv6DuN1iTpOO4xr63c-EZUFoIFTdw05yV0-8gTlBdDTWBieZP6T8ppp--yVBdWVYc4CPs9jcqxARco_MWGMDowytZfCTn0kvpPxRA_Htoel9-6ZDKXOBKwHX_9Sf7F_hVAoEgj_V8NeierlHF6zg4HGodtXsF_Ndgi7cP3oJN34";

        public List<string> ImageUrls { get; set; } = new();

        public int SkipSeconds { get; set; } = 6;

        public const int RecommendedWidth = 1920;
        public const int RecommendedHeight = 720;
    }
}
