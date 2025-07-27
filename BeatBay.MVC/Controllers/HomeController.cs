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

        // **Método privado para verificar autorización de Admin**
        private async Task<bool> IsUserAdminAsync()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/auth/user-roles");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var roles = JsonConvert.DeserializeObject<List<string>>(jsonContent);
                    return roles != null && roles.Contains("Admin");
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // **Método privado para verificar autorización de Artist**
        private async Task<bool> IsUserArtistAsync()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/auth/user-roles");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var roles = JsonConvert.DeserializeObject<List<string>>(jsonContent);
                    return roles != null && roles.Contains("Artist");
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // **Método privado para redirigir si no está autorizado como Admin**
        private async Task<IActionResult> CheckAdminAuthorizationAndRedirectAsync()
        {
            if (!await IsUserAdminAsync())
            {
                TempData["Error"] = "No tienes permisos para acceder al panel de administración";
                return RedirectToAction("Index", "Home");
            }
            return null;
        }

        // **Método privado para redirigir si no está autorizado como Artist**
        private async Task<IActionResult> CheckArtistAuthorizationAndRedirectAsync()
        {
            if (!await IsUserArtistAsync())
            {
                TempData["Error"] = "No tienes permisos para acceder al panel de artista";
                return RedirectToAction("Index", "Home");
            }
            return null;
        }

        public async Task<IActionResult> Index()
        {
            var userDataJson = HttpContext.Session.GetString("UserData");
            UserDto currentUser = null;
            if (!string.IsNullOrEmpty(userDataJson))
            {
                currentUser = JsonConvert.DeserializeObject<UserDto>(userDataJson);
            }

            // Si el usuario está logueado, verificar roles y redirigir
            if (currentUser != null)
            {
                // Verificar si es Admin
                if (await IsUserAdminAsync())
                {
                    return RedirectToAction("IndexAdmin", "Home");
                }

                // Verificar si es Artist
                if (await IsUserArtistAsync())
                {
                    return RedirectToAction("IndexArtista", "Home");
                }

                // Si no es ni Admin ni Artist, mostrar vista normal de usuario
            }

            ViewBag.CurrentUser = currentUser;
            ViewBag.IsLoggedIn = currentUser != null;
            return View();
        }

        // **Vista de Artista protegida**
        public async Task<IActionResult> IndexArtista()
        {
            // Verificar autorización antes de mostrar el panel de artista
            var authCheck = await CheckArtistAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            // Si llega aquí, el usuario es artista
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

        // **Vista de Admin protegida**
        public async Task<IActionResult> IndexAdmin()
        {
            // Verificar autorización antes de mostrar el panel
            var authCheck = await CheckAdminAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            // Si llega aquí, el usuario es admin
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