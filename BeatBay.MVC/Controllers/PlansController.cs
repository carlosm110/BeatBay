using BeatBay.DTOs;
using BeatBay.APIConsumer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BeatBay.MVC.Controllers
{
    public class PlansController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl;

        public PlansController(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _apiBaseUrl = config["ApiSettings:BaseUrl"].TrimEnd('/');

            // Configurar endpoint para Crud<UserDto>
            Crud<UserDto>.EndPoint = $"{_apiBaseUrl}/api/Users";
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_apiBaseUrl);
            var token = HttpContext.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                Crud<UserDto>.AuthToken = token;
            }
            return client;
        }

        private IActionResult HandleUnauthorized()
        {
            HttpContext.Session.Remove("JwtToken");
            TempData["ErrorMessage"] = "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.";
            return RedirectToAction("Login", "vAuth");
        }

        private string ExtractErrorMessage(string json)
        {
            try
            {
                dynamic o = JsonConvert.DeserializeObject<dynamic>(json);
                return o?.message;
            }
            catch
            {
                return null;
            }
        }

        // 1. GET /Plans
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            try
            {
                var client = CreateClient();
                var resp = await client.GetAsync("api/PlanSimulation/my-plan-status");
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    return HandleUnauthorized();
                resp.EnsureSuccessStatusCode();

                var model = JsonConvert.DeserializeObject<UserPlanStatusDto>(
                    await resp.Content.ReadAsStringAsync());
                return View(model);
            }
            catch
            {
                ViewBag.ErrorMessage = "No se pudo conectar con el servidor.";
                return View(new UserPlanStatusDto());
            }
        }

        // 2. GET /Plans/Purchase
        [HttpGet]
        public async Task<IActionResult> Purchase()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            try
            {
                var client = CreateClient();
                var resp = await client.GetAsync("api/PlanSimulation/my-plan-status");
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    return HandleUnauthorized();
                resp.EnsureSuccessStatusCode();

                var model = JsonConvert.DeserializeObject<UserPlanStatusDto>(
                    await resp.Content.ReadAsStringAsync());
                if (!model.CanPurchasePlan)
                {
                    TempData["ErrorMessage"] = model.ReasonCannotPurchase;
                    return RedirectToAction(nameof(Index));
                }
                return View(model);
            }
            catch
            {
                TempData["ErrorMessage"] = "Error al obtener los planes.";
                return RedirectToAction(nameof(Index));
            }
        }

        // 3. POST /Plans/Purchase
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Purchase(int planId)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            var client = CreateClient();
            var returnUrl = Url.Action(nameof(ExecutePayment), "Plans", null, Request.Scheme);
            var cancelUrl = Url.Action(nameof(Cancel), "Plans", null, Request.Scheme);

            var dto = new PurchasePlanDto
            {
                PlanId = planId,
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl
            };

            var resp = await client.PostAsJsonAsync("api/PlanSimulation/purchase", dto);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return HandleUnauthorized();
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                TempData["ErrorMessage"] = ExtractErrorMessage(err) ?? "Error al iniciar compra.";
                return RedirectToAction(nameof(Purchase));
            }

            var wrapper = JsonConvert.DeserializeObject<PurchasePlanResponseDto>(
                await resp.Content.ReadAsStringAsync());
            return Redirect(wrapper.ApprovalUrl);
        }

        // 4. GET /Plans/ExecutePayment
        [HttpGet]
        public async Task<IActionResult> ExecutePayment(string paymentId, string PayerID)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            var client = CreateClient();
            var url = $"api/PlanSimulation/execute-payment?paymentId={WebUtility.UrlEncode(paymentId)}&PayerID={WebUtility.UrlEncode(PayerID)}";
            var resp = await client.GetAsync(url);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return HandleUnauthorized();
            if (!resp.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "No se pudo procesar el pago.";
            else
                TempData["SuccessMessage"] = "Pago completado exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // 5. GET /Plans/Cancel
        [HttpGet]
        public IActionResult Cancel()
        {
            TempData["ErrorMessage"] = "Pago cancelado por el usuario.";
            return RedirectToAction(nameof(Index));
        }

        // 6. GET /Plans/Change
        [HttpGet]
        public async Task<IActionResult> Change()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            try
            {
                var client = CreateClient();
                var resp = await client.GetAsync("api/PlanSimulation/my-plan-status");
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    return HandleUnauthorized();
                resp.EnsureSuccessStatusCode();

                var model = JsonConvert.DeserializeObject<UserPlanStatusDto>(
                    await resp.Content.ReadAsStringAsync());
                if (!model.HasPlan)
                {
                    TempData["ErrorMessage"] = "No tienes un plan activo para cambiar.";
                    return RedirectToAction(nameof(Index));
                }
                return View(model);
            }
            catch
            {
                TempData["ErrorMessage"] = "Error al obtener el estado del plan.";
                return RedirectToAction(nameof(Index));
            }
        }

        // 7. POST /Plans/Change
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Change(int newPlanId)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            var client = CreateClient();
            var dto = new ChangePlanDto { NewPlanId = newPlanId };
            var resp = await client.PostAsJsonAsync("api/PlanSimulation/change", dto);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return HandleUnauthorized();
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                TempData["ErrorMessage"] = ExtractErrorMessage(err) ?? "Error al cambiar de plan.";
                return RedirectToAction(nameof(Change));
            }

            TempData["SuccessMessage"] = "Plan cambiado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // 8. GET /Plans/ManageConnections
        [HttpGet]
        public async Task<IActionResult> ManageConnections()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            var client = CreateClient();
            var respPlan = await client.GetAsync("api/PlanSimulation/my-plan-status");
            if (respPlan.StatusCode == HttpStatusCode.Unauthorized)
                return HandleUnauthorized();
            respPlan.EnsureSuccessStatusCode();

            var planStatus = JsonConvert.DeserializeObject<UserPlanStatusDto>(
                await respPlan.Content.ReadAsStringAsync());

            if (!planStatus.HasPlan)
            {
                TempData["ErrorMessage"] = "No tienes un plan activo.";
                return RedirectToAction(nameof(Index));
            }
            if (planStatus.CurrentSubscription.MaxConnections <= 1)
            {
                TempData["ErrorMessage"] = "Tu plan no permite conexiones adicionales.";
                return RedirectToAction(nameof(Index));
            }

            return View(planStatus);
        }

        // 9. POST /Plans/AddConnection
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConnection(int childUserId)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            var client = CreateClient();
            var dto = new AddConnectionDto { ChildUserId = childUserId };
            var resp = await client.PostAsJsonAsync("api/PlanSimulation/add-connection", dto);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return HandleUnauthorized();

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                TempData["ErrorMessage"] = ExtractErrorMessage(err) ?? "Error al agregar conexión.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Usuario ID {childUserId} agregado exitosamente.";
            }

            return RedirectToAction(nameof(ManageConnections));
        }

        // 10. POST /Plans/RemoveConnection
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveConnection(int childUserId)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            var client = CreateClient();
            var dto = new RemoveConnectionDto { ChildUserId = childUserId };
            var resp = await client.PostAsJsonAsync("api/PlanSimulation/remove-connection", dto);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return HandleUnauthorized();

            if (!resp.IsSuccessStatusCode)
                TempData["ErrorMessage"] = ExtractErrorMessage(await resp.Content.ReadAsStringAsync())
                                          ?? "Error al remover conexión.";
            else
                TempData["SuccessMessage"] = $"Usuario ID {childUserId} removido exitosamente.";

            return RedirectToAction(nameof(ManageConnections));
        }

        // 11. POST /Plans/Cancel (cancelar suscripción)
        [HttpPost, ActionName("Cancel"), ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSubscription()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            var client = CreateClient();
            var resp = await client.PostAsync("api/PlanSimulation/cancel", null);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return HandleUnauthorized();

            if (!resp.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "Error al cancelar suscripción.";
            else
                TempData["SuccessMessage"] = "Suscripción cancelada exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // 12. GET /Plans/History
        [HttpGet]
        public async Task<IActionResult> History()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            var client = CreateClient();
            var resp = await client.GetAsync("api/PlanSimulation/history");
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return HandleUnauthorized();
            resp.EnsureSuccessStatusCode();

            var list = JsonConvert.DeserializeObject<List<PlanSubscriptionDto>>(
                await resp.Content.ReadAsStringAsync());
            return View(list);
        }

        // 13. GET /Plans/SearchUsers
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string username)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return Json(new List<object>());

            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"api/PlanSimulation/search-users?username={Uri.EscapeDataString(username)}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return Json(new List<object>());

                if (!response.IsSuccessStatusCode)
                    return Json(new List<object>());

                var users = JsonConvert.DeserializeObject<List<UserDto>>(
                    await response.Content.ReadAsStringAsync());
                return Json(users);
            }
            catch
            {
                return Json(new List<object>());
            }
        }

        // 14. GET /Plans/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "vAuth");

            try
            {
                var client = CreateClient();

                // Primero obtenemos el historial completo
                var resp = await client.GetAsync("api/PlanSimulation/history");
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    return HandleUnauthorized();
                resp.EnsureSuccessStatusCode();

                var subscriptions = JsonConvert.DeserializeObject<List<PlanSubscriptionDto>>(
                    await resp.Content.ReadAsStringAsync());

                // Buscamos la suscripción específica
                var subscription = subscriptions.FirstOrDefault(s => s.Id == id);

                if (subscription == null)
                {
                    TempData["ErrorMessage"] = "Suscripción no encontrada.";
                    return RedirectToAction(nameof(History));
                }

                return View(subscription);
            }
            catch
            {
                TempData["ErrorMessage"] = "Error al obtener los detalles de la suscripción.";
                return RedirectToAction(nameof(History));
            }
        }
    }
}