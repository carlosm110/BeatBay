using Microsoft.AspNetCore.Mvc;
using BeatBay.DTOs;
using BeatBay.APIConsumer;
using Newtonsoft.Json;
using System.Text;

namespace BeatBayMVC.Controllers
{
    public class VAuthController : Controller
    {
        private readonly string _apiBaseUrl;
        private readonly HttpClient _httpClient;

        public VAuthController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
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
                    // **Corrección: Usar el mismo enfoque que en el método Register**
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    ModelState.AddModelError("", errorResponse?.message?.ToString() ?? "Artist registration failed");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
            }

            return View(model);
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

        // GET: Reset Password
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Invalid reset password link.";
                return RedirectToAction("Login");
            }

            var model = new ResetPasswordDto
            {
                UserId = userId,
                Token = token
            };

            return View(model);
        }

        // POST: Reset Password
        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                return View(model);
            }

            try
            {
                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/reset-password", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Password reset successfully! You can now login with your new password.";
                    return RedirectToAction("Login");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    ModelState.AddModelError("", errorResponse?.message?.ToString() ?? "Password reset failed");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
            }

            return View(model);
        }

        // GET: Confirm Email
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Invalid email confirmation link.";
                return RedirectToAction("Login");
            }

            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/Auth/confirm-email?userId={userId}&token={Uri.EscapeDataString(token)}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Email confirmed successfully! You can now login to your account.";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    TempData["Error"] = errorResponse?.message?.ToString() ?? "Email confirmation failed";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("Login");
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
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Token expirado, intentar refresh
                    if (await TryRefreshToken())
                    {
                        return await Profile(); // Reintentar
                    }

                    TempData["Error"] = "Session expired. Please login again.";
                    return RedirectToAction("Login");
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

        // POST: Update Profile
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(UpdateUserDto model)
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return RedirectToAction("Login");

            var userDataJson = HttpContext.Session.GetString("UserData");
            if (string.IsNullOrEmpty(userDataJson))
                return RedirectToAction("Login");

            var userData = JsonConvert.DeserializeObject<UserDto>(userDataJson);

            if (!ModelState.IsValid)
            {
                // Recargar el perfil para mostrar errores
                return await Profile();
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"{_apiBaseUrl}/Auth/{userData.Id}", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Profile updated successfully!";

                    // Actualizar datos en sesión
                    userData.Name = model.Name ?? userData.Name;
                    userData.Bio = model.Bio ?? userData.Bio;
                    userData.PlanId = model.PlanId ?? userData.PlanId;

                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(userData));
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    TempData["Error"] = errorResponse?.message?.ToString() ?? "Profile update failed";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("Profile");
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

        // Método auxiliar para intentar refresh token
        private async Task<bool> TryRefreshToken()
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                var refreshToken = HttpContext.Session.GetString("RefreshToken");

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(refreshToken))
                    return false;

                var refreshRequest = new RefreshTokenDto
                {
                    Token = token,
                    RefreshToken = refreshToken
                };

                var json = JsonConvert.SerializeObject(refreshRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/refresh-token", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var authResponse = JsonConvert.DeserializeObject<AuthResponseDto>(responseContent);

                    // Actualizar tokens en sesión
                    HttpContext.Session.SetString("JwtToken", authResponse.Token);
                    HttpContext.Session.SetString("RefreshToken", authResponse.RefreshToken);
                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(authResponse.User));

                    return true;
                }
            }
            catch
            {
                // Ignorar errores de refresh token
            }

            return false;
        }

        // Método auxiliar para validar token
        public async Task<IActionResult> ValidateToken()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { isValid = false, message = "No token found" });

            try
            {
                var json = JsonConvert.SerializeObject(token);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/validate-token", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var validationResult = JsonConvert.DeserializeObject<TokenValidationDto>(responseContent);
                    return Json(validationResult);
                }
                else
                {
                    return Json(new { isValid = false, message = "Token validation failed" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { isValid = false, message = ex.Message });
            }
        }

        // Método auxiliar para verificar si el usuario está autenticado
        private bool IsAuthenticated()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            return !string.IsNullOrEmpty(token);
        }

        // Método auxiliar para obtener el usuario actual
        private UserDto GetCurrentUser()
        {
            var userDataJson = HttpContext.Session.GetString("UserData");
            if (!string.IsNullOrEmpty(userDataJson))
            {
                return JsonConvert.DeserializeObject<UserDto>(userDataJson);
            }
            return null;
        }
    }
}