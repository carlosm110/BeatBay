using BeatBay.APIConsumer;
using BeatBay.DTOs;
using BeatBay.Model.DTOs;
using Microsoft.AspNetCore.Mvc;
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

        // Modificar el método Login existente para manejar 2FA
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
                    var authResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);

                    // Verificar si requiere 2FA
                    if (authResponse.requiresTwoFactor == true)
                    {
                        // Enviar código 2FA automáticamente
                        await SendTwoFactorCodeAsync(model.UserName);

                        TempData["Info"] = "Two-factor authentication required. A verification code has been sent to your email.";
                        return RedirectToAction("TwoFactorLogin", new { userName = model.UserName });
                    }

                    // Login normal sin 2FA
                    var fullAuthResponse = JsonConvert.DeserializeObject<AuthResponseDto>(responseContent);
                    HttpContext.Session.SetString("JwtToken", fullAuthResponse.Token);
                    HttpContext.Session.SetString("RefreshToken", fullAuthResponse.RefreshToken);
                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(fullAuthResponse.User));

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

        // Método auxiliar para enviar código 2FA
        private async Task<bool> SendTwoFactorCodeAsync(string userName)
        {
            try
            {
                var resendCodeDto = new ResendCodeDto { UserName = userName };
                var json = JsonConvert.SerializeObject(resendCodeDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/request-2fa-code", content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                // Log del error si es necesario
                Console.WriteLine($"Error sending 2FA code: {ex.Message}");
                return false;
            }
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

            // Validación adicional de confirmación de contraseña
            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Las contraseñas no coinciden.");
                return View(model);
            }

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

                    // Manejar errores específicos de la API
                    if (errorResponse?.errors != null)
                    {
                        foreach (var error in errorResponse.errors)
                        {
                            ModelState.AddModelError("", error.ToString());
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", errorResponse?.message?.ToString() ?? "Registration failed");
                    }
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

            // Validación adicional de confirmación de contraseña
            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Las contraseñas no coinciden.");
                return View(model);
            }

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

                    // Manejar errores específicos de la API
                    if (errorResponse?.errors != null)
                    {
                        foreach (var error in errorResponse.errors)
                        {
                            ModelState.AddModelError("", error.ToString());
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", errorResponse?.message?.ToString() ?? "Artist registration failed");
                    }
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
        public async Task<IActionResult> UpdateProfile(UpdateUserDto model, bool redirect = true)
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
                if (redirect)
                    return await Profile();
                else
                    return Json(new { success = false, message = "Validation failed" });
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
                    // Actualizar datos en sesión
                    userData.Name = model.Name ?? userData.Name;
                    userData.Bio = model.Bio ?? userData.Bio;
                    userData.PlanId = model.PlanId ?? userData.PlanId;

                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(userData));

                    if (redirect)
                    {
                        TempData["Success"] = "Profile updated successfully!";
                        return RedirectToAction("Profile");
                    }
                    else
                    {
                        return Json(new { success = true, message = "Profile updated successfully!" });
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    string errorMessage = errorResponse?.message?.ToString() ?? "Profile update failed";

                    if (redirect)
                    {
                        TempData["Error"] = errorMessage;
                        return RedirectToAction("Profile");
                    }
                    else
                    {
                        return Json(new { success = false, message = errorMessage });
                    }
                }
            }
            catch (Exception ex)
            {
                if (redirect)
                {
                    TempData["Error"] = $"Error: {ex.Message}";
                    return RedirectToAction("Profile");
                }
                else
                {
                    return Json(new { success = false, message = $"Error: {ex.Message}" });
                }
            }
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

        // GET: Two Factor Settings
        public async Task<IActionResult> TwoFactorSettings()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return RedirectToAction("Login");

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/Auth/2fa-status");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var status = JsonConvert.DeserializeObject<dynamic>(responseContent);

                    ViewBag.Is2FAEnabled = status.is2FAEnabled;
                    return View();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (await TryRefreshToken())
                    {
                        return await TwoFactorSettings();
                    }
                    TempData["Error"] = "Session expired. Please login again.";
                    return RedirectToAction("Login");
                }
                else
                {
                    TempData["Error"] = "Unable to load 2FA settings";
                    return RedirectToAction("Profile");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Profile");
            }
        }

        // API ENDPOINT: Get 2FA Status (Para el JavaScript)
        [HttpGet]
        public async Task<IActionResult> Get2FAStatus()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { success = false, is2FAEnabled = false, message = "Not authenticated" });

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/Auth/2fa-status");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    // Asegúrate de que la API devuelve un campo booleano claro
                    var status = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    bool isEnabled = status?.is2FAEnabled ?? false; // Valor predeterminado false

                    return Json(new { success = true, is2FAEnabled = isEnabled });
                }

                return Json(new { success = false, is2FAEnabled = false }); // Fallback seguro
            }
            catch
            {
                return Json(new { success = false, is2FAEnabled = false }); // Fallback seguro
            }
        }

        // API ENDPOINT: Enable 2FA (Para el JavaScript)
        [HttpPost]
        public async Task<IActionResult> Enable2FA([FromBody] Enable2FADto model)
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { success = false, message = "Not authenticated" });

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Please provide a valid password" });
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/enable-2fa", content);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Two-factor authentication enabled successfully!"
                    });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (await TryRefreshToken())
                    {
                        return await Enable2FA(model);
                    }
                    return Json(new { success = false, message = "Session expired" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                        return Json(new { success = false, message = errorResponse?.message?.ToString() ?? "Failed to enable 2FA" });
                    }
                    catch
                    {
                        return Json(new { success = false, message = errorContent ?? "Failed to enable 2FA" });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // API ENDPOINT: Disable 2FA (Para el JavaScript)
        [HttpPost]
        public async Task<IActionResult> Disable2FA([FromBody] Disable2FADto model)
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { success = false, message = "Not authenticated" });

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Please provide a valid password" });
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/disable-2fa", content);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Two-factor authentication disabled successfully!"
                    });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (await TryRefreshToken())
                    {
                        return await Disable2FA(model);
                    }
                    return Json(new { success = false, message = "Session expired" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                        return Json(new { success = false, message = errorResponse?.message?.ToString() ?? "Failed to disable 2FA" });
                    }
                    catch
                    {
                        return Json(new { success = false, message = errorContent ?? "Failed to disable 2FA" });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: Two Factor Login
        public IActionResult TwoFactorLogin(string userName)
        {
            if (string.IsNullOrEmpty(userName))
                return RedirectToAction("Login");

            var model = new Verify2FADto { UserName = userName };
            return View(model);
        }

        // POST: Two Factor Login
        [HttpPost]
        public async Task<IActionResult> TwoFactorLogin(Verify2FADto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/login-2fa", content);

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
                    ModelState.AddModelError("", errorResponse?.message?.ToString() ?? "Two-factor authentication failed");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
            }

            return View(model);
        }

        // POST: Request 2FA Code
        [HttpPost]
        public async Task<IActionResult> Request2FACode([FromBody] ResendCodeDto model)
        {
            try
            {
                var json = JsonConvert.SerializeObject(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/Auth/request-2fa-code", content);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Verification code sent to your email!" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    return Json(new { success = false, message = errorResponse?.message?.ToString() ?? "Failed to send verification code" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }
}