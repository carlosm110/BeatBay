using BeatBay.DTOs;
using BeatBay.APIConsumer;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace BeatBay.MVC.Controllers
{
    public class PlansController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl;

        public PlansController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _apiBaseUrl = _configuration["ApiSettings:BaseUrl"];
        }

        private void ConfigureCrud()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            Crud<UserPlanStatusDto>.AuthToken = token;
            Crud<UserPlanStatusDto>.EndPoint = $"{_apiBaseUrl}/PlanSimulation";
        }

        private bool IsSessionValid()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            return !string.IsNullOrEmpty(token);
        }

        private IActionResult HandleUnauthorized()
        {
            HttpContext.Session.Remove("JwtToken");
            TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
            return RedirectToAction("Login", "vAuth");
        }

        private async Task<T> ExecuteApiCall<T>(Func<Task<T>> apiCall) where T : class, new()
        {
            try
            {
                return await apiCall();
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("401"))
            {
                throw new UnauthorizedAccessException();
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException("La conexión tardó demasiado. Por favor, intenta nuevamente.");
            }
        }

        // Vista principal de planes
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                // Usar el CRUD para obtener el estado del plan
                var planStatus = await Task.Run(() =>
                {
                    // Configurar endpoint específico para esta llamada
                    var originalEndpoint = Crud<UserPlanStatusDto>.EndPoint;
                    Crud<UserPlanStatusDto>.EndPoint = $"{_apiBaseUrl}/PlanSimulation/my-plan-status";

                    try
                    {
                        // Simular GetByEndpoint ya que no existe, usamos una llamada personalizada
                        using var client = new HttpClient();
                        if (!string.IsNullOrEmpty(Crud<UserPlanStatusDto>.AuthToken))
                        {
                            client.DefaultRequestHeaders.Authorization =
                                new AuthenticationHeaderValue("Bearer", Crud<UserPlanStatusDto>.AuthToken);
                        }

                        var response = client.GetAsync(Crud<UserPlanStatusDto>.EndPoint).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            var json = response.Content.ReadAsStringAsync().Result;
                            return JsonConvert.DeserializeObject<UserPlanStatusDto>(json);
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            throw new UnauthorizedAccessException();
                        }
                        else
                        {
                            throw new Exception($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                        }
                    }
                    finally
                    {
                        Crud<UserPlanStatusDto>.EndPoint = originalEndpoint;
                    }
                });

                return View(planStatus);
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                ViewBag.ErrorMessage = ex.Message;
                return View(new UserPlanStatusDto());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Error: {ex.Message}";
                return View(new UserPlanStatusDto());
            }
        }

        // Vista para comprar plan
        [HttpGet]
        public async Task<IActionResult> Purchase()
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                var planStatus = await GetPlanStatus();

                if (!planStatus.CanPurchasePlan)
                {
                    TempData["ErrorMessage"] = planStatus.ReasonCannotPurchase;
                    return RedirectToAction("Index");
                }

                return View(planStatus);
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Procesar compra de plan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Purchase(int planId)
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                var purchaseDto = new PurchasePlanDto { PlanId = planId };

                // Usar el CRUD para crear/enviar la compra
                var result = await Task.Run(() =>
                {
                    var originalEndpoint = Crud<PurchasePlanDto>.EndPoint;
                    Crud<PurchasePlanDto>.AuthToken = Crud<UserPlanStatusDto>.AuthToken;
                    Crud<PurchasePlanDto>.EndPoint = $"{_apiBaseUrl}/PlanSimulation/purchase";

                    try
                    {
                        return Crud<PurchasePlanDto>.Create(purchaseDto);
                    }
                    finally
                    {
                        Crud<PurchasePlanDto>.EndPoint = originalEndpoint;
                    }
                });

                TempData["SuccessMessage"] = "¡Plan comprado exitosamente!";
                return RedirectToAction("Index");
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Purchase");
            }
            catch (Exception ex)
            {
                // Extraer mensaje de error si es posible
                var errorMessage = ExtractErrorMessage(ex.Message);
                TempData["ErrorMessage"] = errorMessage ?? "Error al comprar el plan";
                return RedirectToAction("Purchase");
            }
        }

        // Vista para cambiar plan
        [HttpGet]
        public async Task<IActionResult> Change()
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                var planStatus = await GetPlanStatus();

                if (!planStatus.HasPlan)
                {
                    TempData["ErrorMessage"] = "No tienes un plan activo para cambiar";
                    return RedirectToAction("Index");
                }

                return View(planStatus);
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Procesar cambio de plan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Change(int newPlanId)
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                var changeDto = new ChangePlanDto { NewPlanId = newPlanId };

                var result = await Task.Run(() =>
                {
                    var originalEndpoint = Crud<ChangePlanDto>.EndPoint;
                    Crud<ChangePlanDto>.AuthToken = Crud<UserPlanStatusDto>.AuthToken;
                    Crud<ChangePlanDto>.EndPoint = $"{_apiBaseUrl}/PlanSimulation/change";

                    try
                    {
                        return Crud<ChangePlanDto>.Create(changeDto);
                    }
                    finally
                    {
                        Crud<ChangePlanDto>.EndPoint = originalEndpoint;
                    }
                });

                TempData["SuccessMessage"] = "¡Plan cambiado exitosamente!";
                return RedirectToAction("Index");
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Change");
            }
            catch (Exception ex)
            {
                var errorMessage = ExtractErrorMessage(ex.Message);
                TempData["ErrorMessage"] = errorMessage ?? "Error al cambiar el plan";
                return RedirectToAction("Change");
            }
        }

        // Vista para gestionar conexiones
        [HttpGet]
        public async Task<IActionResult> ManageConnections()
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                var planStatus = await GetPlanStatus();

                if (!planStatus.HasPlan)
                {
                    TempData["ErrorMessage"] = "No tienes un plan activo";
                    return RedirectToAction("Index");
                }

                if (planStatus.CurrentSubscription.MaxConnections <= 1)
                {
                    TempData["ErrorMessage"] = "Tu plan no permite conexiones adicionales";
                    return RedirectToAction("Index");
                }

                return View(planStatus);
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Agregar conexión
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConnection(int childUserId)
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                var addDto = new AddConnectionDto { ChildUserId = childUserId };

                var result = await Task.Run(() =>
                {
                    var originalEndpoint = Crud<AddConnectionDto>.EndPoint;
                    Crud<AddConnectionDto>.AuthToken = Crud<UserPlanStatusDto>.AuthToken;
                    Crud<AddConnectionDto>.EndPoint = $"{_apiBaseUrl}/PlanSimulation/add-connection";

                    try
                    {
                        return Crud<AddConnectionDto>.Create(addDto);
                    }
                    finally
                    {
                        Crud<AddConnectionDto>.EndPoint = originalEndpoint;
                    }
                });

                TempData["SuccessMessage"] = "Usuario agregado exitosamente al plan";
                return RedirectToAction("ManageConnections");
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("ManageConnections");
            }
            catch (Exception ex)
            {
                var errorMessage = ExtractErrorMessage(ex.Message);
                TempData["ErrorMessage"] = errorMessage ?? "Error al agregar usuario";
                return RedirectToAction("ManageConnections");
            }
        }

        // Remover conexión
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveConnection(int childUserId)
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                var removeDto = new RemoveConnectionDto { ChildUserId = childUserId };

                var result = await Task.Run(() =>
                {
                    var originalEndpoint = Crud<RemoveConnectionDto>.EndPoint;
                    Crud<RemoveConnectionDto>.AuthToken = Crud<UserPlanStatusDto>.AuthToken;
                    Crud<RemoveConnectionDto>.EndPoint = $"{_apiBaseUrl}/PlanSimulation/remove-connection";

                    try
                    {
                        return Crud<RemoveConnectionDto>.Create(removeDto);
                    }
                    finally
                    {
                        Crud<RemoveConnectionDto>.EndPoint = originalEndpoint;
                    }
                });

                TempData["SuccessMessage"] = "Usuario removido exitosamente del plan";
                return RedirectToAction("ManageConnections");
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("ManageConnections");
            }
            catch (Exception ex)
            {
                var errorMessage = ExtractErrorMessage(ex.Message);
                TempData["ErrorMessage"] = errorMessage ?? "Error al remover usuario";
                return RedirectToAction("ManageConnections");
            }
        }

        // Cancelar suscripción
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel()
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                var result = await Task.Run(() =>
                {
                    // Usar una llamada personalizada ya que es POST sin cuerpo
                    using var client = new HttpClient();
                    if (!string.IsNullOrEmpty(Crud<UserPlanStatusDto>.AuthToken))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", Crud<UserPlanStatusDto>.AuthToken);
                    }

                    var response = client.PostAsync($"{_apiBaseUrl}/PlanSimulation/cancel", null).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new UnauthorizedAccessException();
                    }
                    else
                    {
                        throw new Exception($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                });

                TempData["SuccessMessage"] = "Suscripción cancelada exitosamente";
                return RedirectToAction("Index");
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                var errorMessage = ExtractErrorMessage(ex.Message);
                TempData["ErrorMessage"] = errorMessage ?? "Error al cancelar suscripción";
                return RedirectToAction("Index");
            }
        }

        // Ver historial de suscripciones
        [HttpGet]
        public async Task<IActionResult> History()
        {
            if (!IsSessionValid())
            {
                TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
                return RedirectToAction("Login", "vAuth");
            }

            try
            {
                ConfigureCrud();

                var history = await Task.Run(() =>
                {
                    var originalEndpoint = Crud<List<PlanSubscriptionDto>>.EndPoint;
                    Crud<List<PlanSubscriptionDto>>.AuthToken = Crud<UserPlanStatusDto>.AuthToken;
                    Crud<List<PlanSubscriptionDto>>.EndPoint = $"{_apiBaseUrl}/PlanSimulation/history";

                    try
                    {
                        // Usar una llamada personalizada para obtener lista
                        using var client = new HttpClient();
                        if (!string.IsNullOrEmpty(Crud<List<PlanSubscriptionDto>>.AuthToken))
                        {
                            client.DefaultRequestHeaders.Authorization =
                                new AuthenticationHeaderValue("Bearer", Crud<List<PlanSubscriptionDto>>.AuthToken);
                        }

                        var response = client.GetAsync(Crud<List<PlanSubscriptionDto>>.EndPoint).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            var json = response.Content.ReadAsStringAsync().Result;
                            return JsonConvert.DeserializeObject<List<PlanSubscriptionDto>>(json);
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            throw new UnauthorizedAccessException();
                        }
                        else
                        {
                            throw new Exception($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                        }
                    }
                    finally
                    {
                        Crud<List<PlanSubscriptionDto>>.EndPoint = originalEndpoint;
                    }
                });

                return View(history ?? new List<PlanSubscriptionDto>());
            }
            catch (UnauthorizedAccessException)
            {
                return HandleUnauthorized();
            }
            catch (TimeoutException ex)
            {
                ViewBag.ErrorMessage = ex.Message;
                return View(new List<PlanSubscriptionDto>());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Error: {ex.Message}";
                return View(new List<PlanSubscriptionDto>());
            }
        }

        // Métodos auxiliares
        private async Task<UserPlanStatusDto> GetPlanStatus()
        {
            return await Task.Run(() =>
            {
                var originalEndpoint = Crud<UserPlanStatusDto>.EndPoint;
                Crud<UserPlanStatusDto>.EndPoint = $"{_apiBaseUrl}/PlanSimulation/my-plan-status";

                try
                {
                    using var client = new HttpClient();
                    if (!string.IsNullOrEmpty(Crud<UserPlanStatusDto>.AuthToken))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", Crud<UserPlanStatusDto>.AuthToken);
                    }

                    var response = client.GetAsync(Crud<UserPlanStatusDto>.EndPoint).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var json = response.Content.ReadAsStringAsync().Result;
                        return JsonConvert.DeserializeObject<UserPlanStatusDto>(json);
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new UnauthorizedAccessException();
                    }
                    else
                    {
                        throw new Exception($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
                finally
                {
                    Crud<UserPlanStatusDto>.EndPoint = originalEndpoint;
                }
            });
        }

        private string ExtractErrorMessage(string exceptionMessage)
        {
            try
            {
                // Intenta extraer el mensaje JSON de error si está presente
                var startIndex = exceptionMessage.IndexOf("{");
                if (startIndex >= 0)
                {
                    var jsonPart = exceptionMessage.Substring(startIndex);
                    var endIndex = jsonPart.LastIndexOf("}") + 1;
                    if (endIndex > 0)
                    {
                        jsonPart = jsonPart.Substring(0, endIndex);
                        var errorResponse = JsonConvert.DeserializeObject<dynamic>(jsonPart);
                        return errorResponse?.message?.ToString();
                    }
                }
            }
            catch
            {
                // Si no se puede parsear, devolver null
            }
            return null;
        }
    }
}