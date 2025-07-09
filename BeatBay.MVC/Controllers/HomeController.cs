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

        public async Task<IActionResult> Index()
        {
            // Obtener datos del usuario desde la sesión
            var userDataJson = HttpContext.Session.GetString("UserData");
            UserDto currentUser = null;

            if (!string.IsNullOrEmpty(userDataJson))
            {
                currentUser = JsonConvert.DeserializeObject<UserDto>(userDataJson);
            }

            ViewBag.CurrentUser = currentUser;
            ViewBag.IsLoggedIn = currentUser != null;

            // Obtener el tema desde la API
            var theme = await GetThemeFromApiAsync();
            ViewBag.Theme = theme;  // Pasar el tema a la vista

            return View();
        }

        public async Task<string> GetThemeFromApiAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{_apiBaseUrl}/theme");  // URL de tu API para el tema
                return response;
            }
            catch (Exception ex)
            {
                // En caso de error, logueamos y retornamos el modo claro por defecto
                _logger.LogError("Error al obtener el tema desde la API", ex);
                return "light";  // Modo claro por defecto
            }
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
