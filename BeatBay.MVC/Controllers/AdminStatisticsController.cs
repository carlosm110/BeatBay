using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Newtonsoft.Json;

namespace BeatBay.MVC.Controllers
{
    public class AdminStatisticsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _baseApiUrl;

        public AdminStatisticsController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _baseApiUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7037/api";
        }

        private HttpClient CreateHttpClient()
        {
            var client = _httpClientFactory.CreateClient();
            var token = HttpContext.Session.GetString("JwtToken"); // Cambié de "token" a "JwtToken" como en AdminController

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            }

            return client;
        }

        // **Método de autorización basado en AdminController**
        private async Task<bool> IsUserAuthorizedAsync()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                using var client = CreateHttpClient();
                var response = await client.GetAsync($"{_baseApiUrl}/auth/user-roles");

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var roles = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(jsonContent);
                    return roles != null && roles.Contains("Admin");
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // **Método de verificación y redirección**
        private async Task<IActionResult?> CheckAuthorizationAndRedirectAsync()
        {
            if (!await IsUserAuthorizedAsync())
            {
                TempData["Error"] = "No tienes permisos para acceder a esta sección";
                return RedirectToAction("Index", "Home");
            }
            return null;
        }

        public async Task<IActionResult> Index()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> General()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                using var client = CreateHttpClient();
                var response = await client.GetAsync($"{_baseApiUrl}/AdminStatistics/general");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    // Log para debug
                    System.Console.WriteLine($"API Response: {json}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var stats = System.Text.Json.JsonSerializer.Deserialize<object>(json, options);
                    return Json(stats);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Json(new { success = false, message = "No tienes permisos para acceder a esta información" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return StatusCode((int)response.StatusCode, $"Error obteniendo estadísticas: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Exception in General: {ex.Message}");
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Songs()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                using var client = CreateHttpClient();
                var response = await client.GetAsync($"{_baseApiUrl}/AdminStatistics/songs");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Songs API Response: {json}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var stats = System.Text.Json.JsonSerializer.Deserialize<object>(json, options);
                    return Json(stats);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Json(new { success = false, message = "No tienes permisos para acceder a esta información" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Songs API Error: {response.StatusCode} - {errorContent}");
                    return StatusCode((int)response.StatusCode, $"Error obteniendo estadísticas de canciones: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Exception in Songs: {ex.Message}");
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                using var client = CreateHttpClient();
                var response = await client.GetAsync($"{_baseApiUrl}/AdminStatistics/users");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Users API Response: {json}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var stats = System.Text.Json.JsonSerializer.Deserialize<object>(json, options);
                    return Json(stats);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Json(new { success = false, message = "No tienes permisos para acceder a esta información" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Users API Error: {response.StatusCode} - {errorContent}");
                    return StatusCode((int)response.StatusCode, $"Error obteniendo estadísticas de usuarios: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Exception in Users: {ex.Message}");
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Playback()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                using var client = CreateHttpClient();
                var response = await client.GetAsync($"{_baseApiUrl}/AdminStatistics/playback");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Playback API Response: {json}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var stats = System.Text.Json.JsonSerializer.Deserialize<object>(json, options);
                    return Json(stats);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Json(new { success = false, message = "No tienes permisos para acceder a esta información" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Playback API Error: {response.StatusCode} - {errorContent}");
                    return StatusCode((int)response.StatusCode, $"Error obteniendo estadísticas de reproducción: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Exception in Playback: {ex.Message}");
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Payments()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                using var client = CreateHttpClient();
                var response = await client.GetAsync($"{_baseApiUrl}/AdminStatistics/payments");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Payments API Response: {json}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var stats = System.Text.Json.JsonSerializer.Deserialize<object>(json, options);
                    return Json(stats);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Json(new { success = false, message = "No tienes permisos para acceder a esta información" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Payments API Error: {response.StatusCode} - {errorContent}");
                    return StatusCode((int)response.StatusCode, $"Error obteniendo estadísticas de pagos: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Exception in Payments: {ex.Message}");
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                using var client = CreateHttpClient();
                var response = await client.GetAsync($"{_baseApiUrl}/AdminStatistics/dashboard");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Dashboard API Response: {json}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var stats = System.Text.Json.JsonSerializer.Deserialize<object>(json, options);
                    return Json(stats);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Json(new { success = false, message = "No tienes permisos para acceder a esta información" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Dashboard API Error: {response.StatusCode} - {errorContent}");
                    return StatusCode((int)response.StatusCode, $"Error obteniendo estadísticas del dashboard: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Exception in Dashboard: {ex.Message}");
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        // Vistas - También con verificación de autorización
        public async Task<IActionResult> DashboardView()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            return View("Dashboard");
        }

        public async Task<IActionResult> SongsView()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            return View("Songs");
        }

        public async Task<IActionResult> UsersView()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            return View("Users");
        }

        public async Task<IActionResult> PlaybackView()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            return View("Playback");
        }

        public async Task<IActionResult> PaymentsView()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            return View("Payments");
        }


        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                using var client = CreateHttpClient();

                // Hacer llamadas paralelas a diferentes endpoints para obtener todos los datos
                var generalTask = client.GetAsync($"{_baseApiUrl}/AdminStatistics/general");
                var songsTask = client.GetAsync($"{_baseApiUrl}/AdminStatistics/songs");
                var usersTask = client.GetAsync($"{_baseApiUrl}/AdminStatistics/users");
                var playbackTask = client.GetAsync($"{_baseApiUrl}/AdminStatistics/playback");

                var responses = await Task.WhenAll(generalTask, songsTask, usersTask, playbackTask);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Procesar las respuestas
                string generalJson = null, songsJson = null, usersJson = null, playbackJson = null;
                object generalStats = null, songsStats = null, usersStats = null, playbackStats = null;

                if (responses[0].IsSuccessStatusCode)
                {
                    generalJson = await responses[0].Content.ReadAsStringAsync();
                    System.Console.WriteLine($"General API Response: {generalJson}");
                    generalStats = System.Text.Json.JsonSerializer.Deserialize<object>(generalJson, options);
                }

                if (responses[1].IsSuccessStatusCode)
                {
                    songsJson = await responses[1].Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Songs API Response: {songsJson}");
                    songsStats = System.Text.Json.JsonSerializer.Deserialize<object>(songsJson, options);
                }

                if (responses[2].IsSuccessStatusCode)
                {
                    usersJson = await responses[2].Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Users API Response: {usersJson}");
                    usersStats = System.Text.Json.JsonSerializer.Deserialize<object>(usersJson, options);
                }

                if (responses[3].IsSuccessStatusCode)
                {
                    playbackJson = await responses[3].Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Playback API Response: {playbackJson}");
                    playbackStats = System.Text.Json.JsonSerializer.Deserialize<object>(playbackJson, options);
                }

                // Crear el objeto de respuesta con los nombres que espera el JavaScript
                var totalUsers = GetPropertyValue(usersStats, "totalUsers") ?? GetPropertyValue(generalStats, "totalUsers") ?? 0;
                var totalSongs = GetPropertyValue(songsStats, "totalSongs") ?? GetPropertyValue(generalStats, "totalSongs") ?? 0;
                var totalPlays = GetPropertyValue(playbackStats, "totalPlays") ?? GetPropertyValue(generalStats, "totalPlays") ?? 0;

                System.Console.WriteLine($"Extracted values - Users: {totalUsers}, Songs: {totalSongs}, Plays: {totalPlays}");

                var dashboardData = new
                {
                    totalUsers = totalUsers,
                    totalSongs = totalSongs,
                    totalPlays = totalPlays,
                    lastUpdateMinutes = 1, // Por ahora fijo, puedes hacerlo dinámico
                    systemStatus = new
                    {
                        isHealthy = true // Por ahora fijo, puedes hacerlo dinámico
                    }
                };

                return Json(dashboardData);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Exception in GetDashboardData: {ex.Message}");

                // Devolver datos por defecto en caso de error
                return Json(new
                {
                    totalUsers = 0,
                    totalSongs = 0,
                    totalPlays = 0,
                    lastUpdateMinutes = 0,
                    systemStatus = new
                    {
                        isHealthy = false
                    }
                });
            }
        }

        // Método helper para extraer valores de los objetos dinámicos
        private object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null) return null;

            try
            {
                if (obj is JsonElement jsonElement)
                {
                    if (jsonElement.TryGetProperty(propertyName, out var property))
                    {
                        return property.GetInt32();
                    }
                }
                else
                {
                    var type = obj.GetType();
                    var prop = type.GetProperty(propertyName);
                    if (prop != null)
                    {
                        return prop.GetValue(obj);
                    }
                }
            }
            catch
            {
                // Ignorar errores y devolver null
            }

            return null;
        }














        [HttpGet]
        public async Task<IActionResult> AllStatistics()
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                using var client = CreateHttpClient();

                var generalTask = client.GetAsync($"{_baseApiUrl}/AdminStatistics/general");
                var songsTask = client.GetAsync($"{_baseApiUrl}/AdminStatistics/songs");
                var usersTask = client.GetAsync($"{_baseApiUrl}/AdminStatistics/users");
                var playbackTask = client.GetAsync($"{_baseApiUrl}/AdminStatistics/playback");
                var paymentsTask = client.GetAsync($"{_baseApiUrl}/AdminStatistics/payments");

                var responses = await Task.WhenAll(generalTask, songsTask, usersTask, playbackTask, paymentsTask);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var allStats = new
                {
                    General = responses[0].IsSuccessStatusCode ?
                        System.Text.Json.JsonSerializer.Deserialize<object>(await responses[0].Content.ReadAsStringAsync(), options) : null,
                    Songs = responses[1].IsSuccessStatusCode ?
                        System.Text.Json.JsonSerializer.Deserialize<object>(await responses[1].Content.ReadAsStringAsync(), options) : null,
                    Users = responses[2].IsSuccessStatusCode ?
                        System.Text.Json.JsonSerializer.Deserialize<object>(await responses[2].Content.ReadAsStringAsync(), options) : null,
                    Playback = responses[3].IsSuccessStatusCode ?
                        System.Text.Json.JsonSerializer.Deserialize<object>(await responses[3].Content.ReadAsStringAsync(), options) : null,
                    Payments = responses[4].IsSuccessStatusCode ?
                        System.Text.Json.JsonSerializer.Deserialize<object>(await responses[4].Content.ReadAsStringAsync(), options) : null
                };

                return Json(allStats);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Exception in AllStatistics: {ex.Message}");
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }
    }
}