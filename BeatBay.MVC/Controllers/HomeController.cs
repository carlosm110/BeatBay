using Microsoft.AspNetCore.Mvc;
using BeatBay.DTOs;
using Newtonsoft.Json;

namespace BeatBayMVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _apiBaseUrl;
        private readonly HttpClient _httpClient;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _apiBaseUrl = configuration.GetSection("ApiSettings:BaseUrl").Value ?? "https://localhost:7037/api";
            _httpClient = httpClientFactory.CreateClient();
        }

        public IActionResult Index()
        {
            var userDataJson = HttpContext.Session.GetString("UserData");
            UserDto currentUser = null;

            if (!string.IsNullOrEmpty(userDataJson))
            {
                currentUser = JsonConvert.DeserializeObject<UserDto>(userDataJson);
            }

            ViewBag.CurrentUser = currentUser;
            ViewBag.IsLoggedIn = currentUser != null;

            return View();
        }

        public IActionResult IndexArtista()
        {
            return View();
        }
        public IActionResult IndexAdmin()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}