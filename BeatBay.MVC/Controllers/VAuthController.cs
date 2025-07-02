using Microsoft.AspNetCore.Mvc;
using BeatBay.DTOs;
using BeatBay.APIConsumer;
using Newtonsoft.Json;
using System.Text;

namespace BeatBayMVC.Controllers
{
    public class AuthController : Controller
    {
        private readonly string _apiBaseUrl;
        private readonly HttpClient _httpClient;

        public AuthController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _apiBaseUrl = configuration.GetSection("ApiSettings:BaseUrl").Value ?? "https://localhost:7037/api";
            _httpClient = httpClientFactory.CreateClient();
        }

        // GET: Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: Login
        [HttpPost]
        public async Task<IActionResult> Login(LoginDto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var authResponse = JsonConvert.DeserializeObject<AuthResponseDto>(responseContent);

                    // Guardar token en sesión
                    HttpContext.Session.SetString("JwtToken", authResponse.Token);
                    HttpContext.Session.SetString("RefreshToken", authResponse.RefreshToken);
                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(authResponse.User));

                    TempData["Success"] = "Login successful!";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    ModelState.AddModelError("", errorResponse?.message?.ToString() ?? "Login failed");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
            }

            return View(model);
        }

        // GET: Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: Register
        [HttpPost]
        public async Task<IActionResult> Register(CreateUserDto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/register", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Registration successful! Please check your email to confirm your account.";
                    return RedirectToAction("Login");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    ModelState.AddModelError("", errorResponse?.message?.ToString() ?? "Registration failed");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
            }

            return View(model);
        }

        // GET: Register Artist
        public IActionResult RegisterArtist()
        {
            return View();
        }

        // POST: Register Artist
        [HttpPost]
        public async Task<IActionResult> RegisterArtist(CreateUserDto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/register-artist", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Artist registration successful! Please check your email to confirm your account.";
                    return RedirectToAction("Login");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    ModelState.AddModelError("", errorResponse?.message?.ToString() ?? "Registration failed");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
            }

            return View(model);
        }

        // Logout
        public async Task<IActionResult> Logout()
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/logout", null);
                }
            }
            catch
            {
                // Ignorar errores de logout en la API
            }
            finally
            {
                // Limpiar sesión local
                HttpContext.Session.Clear();
            }

            TempData["Success"] = "Logged out successfully!";
            return RedirectToAction("Index", "Home");
        }

        // GET: Forgot Password
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: Forgot Password
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/forgot-password", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Password reset link sent to your email!";
                    return RedirectToAction("Login");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    ModelState.AddModelError("", errorResponse?.message?.ToString() ?? "Error sending reset link");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
            }

            return View(model);
        }

        // GET: Profile
        public async Task<IActionResult> Profile()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return RedirectToAction("Login");

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/Auth/profile");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var user = JsonConvert.DeserializeObject<UserDto>(responseContent);
                    return View(user);
                }
                else
                {
                    TempData["Error"] = "Unable to load profile";
                    return RedirectToAction("Login");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Login");
            }
        }
    }
}