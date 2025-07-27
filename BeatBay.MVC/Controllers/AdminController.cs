using BeatBay.APIConsumer;
using BeatBay.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace BeatBay.MVC.Controllers
{
    public class AdminController : Controller
    {
        private readonly string _baseApiUrl;
        private readonly HttpClient _httpClient;

        public AdminController(IConfiguration configuration, HttpClient httpClient)
        {
            _baseApiUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7037/api";
            _httpClient = httpClient;
        }

        // **Método privado para verificar autorización**
        private async Task<bool> IsUserAuthorizedAsync()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.GetAsync($"{_baseApiUrl}/auth/user-roles");
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

        // **Método privado para redirigir si no está autorizado**
        private async Task<IActionResult> CheckAuthorizationAndRedirectAsync()
        {
            if (!await IsUserAuthorizedAsync())
            {
                TempData["Error"] = "No tienes permisos para acceder a esta sección";
                return RedirectToAction("Index", "Home");
            }
            return null;
        }

        // **Vista Principal - Lista de Usuarios CON BÚSQUEDA CORREGIDA**
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int size = 20, bool? isActive = null, string search = "")
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                // Construir query parameters
                var queryParams = new List<string>
                {
                    $"page={page}",
                    $"size={size}"
                };

                if (isActive.HasValue)
                    queryParams.Add($"isActive={isActive.Value}");

                // CORREGIDO: Ahora el API soporta búsqueda
                if (!string.IsNullOrEmpty(search))
                    queryParams.Add($"search={Uri.EscapeDataString(search)}");

                var queryString = string.Join("&", queryParams);
                var url = $"{_baseApiUrl}/admin/users?{queryString}";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<dynamic>(jsonContent);
                    var users = JsonConvert.DeserializeObject<List<UserDto>>(apiResponse.users.ToString());

                    ViewBag.CurrentPage = page;
                    ViewBag.PageSize = size;
                    ViewBag.IsActiveFilter = isActive;
                    ViewBag.SearchFilter = search ?? "";
                    ViewBag.Pagination = apiResponse.pagination;

                    return View(users);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "No tienes permisos para realizar esta acción";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    TempData["Error"] = "Error al cargar usuarios desde la API";
                    return View(new List<UserDto>());
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar usuarios: " + ex.Message;
                return View(new List<UserDto>());
            }
        }

        // **Activar Usuario**
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateUser(int userId)
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.PutAsync($"{_baseApiUrl}/admin/activate-user/{userId}", null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Usuario activado exitosamente";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "No tienes permisos para realizar esta acción";
                    return RedirectToAction("Index", "Home");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                        // CORREGIDO: Convertir explícitamente a string
                        TempData["Error"] = errorResponse?.message?.ToString() ?? "Error al activar usuario";
                    }
                    catch
                    {
                        // Si falla la deserialización, usar el contenido raw o mensaje por defecto
                        TempData["Error"] = !string.IsNullOrEmpty(errorContent) ? errorContent : "Error al activar usuario";
                    }
                }
                else
                {
                    TempData["Error"] = "Error al activar usuario";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al activar usuario: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // **Desactivar Usuario - CON PROTECCIÓN MEJORADA Y ARREGLADO**
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateUser(int userId)
        {
            var authCheck = await CheckAuthorizationAndRedirectAsync();
            if (authCheck != null) return authCheck;

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.PutAsync($"{_baseApiUrl}/admin/deactivate-user/{userId}", null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Usuario desactivado exitosamente";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "No tienes permisos para realizar esta acción";
                    return RedirectToAction("Index", "Home");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                        // CORREGIDO: Convertir explícitamente a string para evitar JValue en TempData
                        TempData["Error"] = errorResponse?.message?.ToString() ?? "Error al desactivar usuario";
                    }
                    catch
                    {
                        // Si falla la deserialización, usar el contenido raw o mensaje por defecto
                        TempData["Error"] = !string.IsNullOrEmpty(errorContent) ? errorContent : "Error al desactivar usuario";
                    }
                }
                else
                {
                    TempData["Error"] = "Error al desactivar usuario";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al desactivar usuario: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }




        // **Obtener detalles de un usuario específico**
        [HttpGet]
        public async Task<IActionResult> GetUserStatus(int userId)
        {
            if (!await IsUserAuthorizedAsync())
            {
                return Json(new { success = false, message = "No tienes permisos para realizar esta acción" });
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.GetAsync($"{_baseApiUrl}/admin/user-status/{userId}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var user = JsonConvert.DeserializeObject<UserDto>(jsonContent);

                    return Json(new
                    {
                        success = true,
                        user = new
                        {
                            id = user.Id,
                            userName = user.UserName,
                            email = user.Email,
                            name = user.Name,
                            isActive = user.IsActive,
                            createdAt = user.CreatedAt,
                            planName = user.PlanName
                        }
                    });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                         response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Json(new { success = false, message = "No tienes permisos para realizar esta acción" });
                }
                else
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}