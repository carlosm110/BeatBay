using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using BeatBay.DTOs;
using BeatBay.APIConsumer;

namespace BeatBayMVC.Controllers
{
    public class ArtistStatisticsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public ArtistStatisticsController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiBaseUrl = configuration.GetValue<string>("ApiSettings:BaseUrl") ?? "https://localhost:7037/api";
        }

        private void SetupCrudAuth()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            Crud<object>.AuthToken = token;
        }

        private async Task<bool> IsUserLoggedInAsync()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/auth/profile");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<List<string>> GetUserRolesAsync()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return new List<string>();

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/auth/user-roles");

                if (!response.IsSuccessStatusCode)
                    return new List<string>();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private async Task<bool> UserHasRoleAsync(string role)
        {
            var userRoles = await GetUserRolesAsync();
            return userRoles.Contains(role);
        }

        // GET: ArtistStatistics
        public async Task<IActionResult> Index()
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para ver estadísticas.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista para acceder a esta sección.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    TempData["Error"] = "Token de sesión no encontrado.";
                    return RedirectToAction("Login", "Auth");
                }

                // Configurar autenticación para Crud
                Crud<object>.AuthToken = token;

                // Obtener resumen usando HttpClient directo porque es un endpoint específico
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var summaryResponse = await _httpClient.GetAsync($"{_apiBaseUrl}/artiststatistics/summary");
                if (!summaryResponse.IsSuccessStatusCode)
                {
                    TempData["Error"] = $"Error al obtener resumen: {summaryResponse.StatusCode}";
                    return View(new List<SongStatsDto>());
                }

                var summaryJson = await summaryResponse.Content.ReadAsStringAsync();
                var summary = JsonConvert.DeserializeObject<dynamic>(summaryJson);

                // Obtener estadísticas de canciones usando Crud
                // Crear un HttpClient personalizado para verificar la respuesta
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    var songsResponse = await client.GetAsync($"{_apiBaseUrl}/artiststatistics/my-songs-stats");
                    if (!songsResponse.IsSuccessStatusCode)
                    {
                        TempData["Error"] = $"Error al obtener canciones: {songsResponse.StatusCode}";
                        return View(new List<SongStatsDto>());
                    }

                    var songsJson = await songsResponse.Content.ReadAsStringAsync();
                    var songs = JsonConvert.DeserializeObject<List<SongStatsDto>>(songsJson) ?? new List<SongStatsDto>();

                    // Obtener top canciones
                    var topSongsResponse = await client.GetAsync($"{_apiBaseUrl}/artiststatistics/top-songs?limit=5");
                    var topSongsJson = await topSongsResponse.Content.ReadAsStringAsync();
                    var topSongs = JsonConvert.DeserializeObject<TopSongsDto>(topSongsJson) ?? new TopSongsDto();

                    ViewBag.Summary = summary;
                    ViewBag.TopSongs = topSongs.Songs;

                    return View(songs);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar estadísticas: {ex.Message}";
                return View(new List<SongStatsDto>());
            }
        }

        // GET: ArtistStatistics/SongDetails/5
        public async Task<IActionResult> SongDetails(int id)
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para ver estadísticas.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista para acceder a esta sección.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    TempData["Error"] = "Token de sesión no encontrado.";
                    return RedirectToAction("Login", "Auth");
                }

                // Usar HttpClient directamente para este endpoint específico
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    var response = await client.GetAsync($"{_apiBaseUrl}/artiststatistics/song-details/{id}");

                    if (!response.IsSuccessStatusCode)
                    {
                        TempData["Error"] = $"No se pudieron obtener los detalles de la canción. Status: {response.StatusCode}";
                        return RedirectToAction(nameof(Index));
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var playbackStats = JsonConvert.DeserializeObject<List<PlaybackStatisticDto>>(json) ?? new List<PlaybackStatisticDto>();

                    ViewBag.SongId = id;
                    ViewBag.SongTitle = playbackStats.FirstOrDefault()?.SongTitle ?? "Canción desconocida";

                    return View(playbackStats);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar detalles: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: ArtistStatistics/TopSongs
        public async Task<IActionResult> TopSongs()
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para ver estadísticas.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista para acceder a esta sección.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    TempData["Error"] = "Token de sesión no encontrado.";
                    return RedirectToAction("Login", "Auth");
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    var response = await client.GetAsync($"{_apiBaseUrl}/artiststatistics/top-songs?limit=20");

                    if (!response.IsSuccessStatusCode)
                    {
                        TempData["Error"] = $"Error al obtener top canciones: {response.StatusCode}";
                        return View(new List<SongStatsDto>());
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var topSongs = JsonConvert.DeserializeObject<TopSongsDto>(json) ?? new TopSongsDto();

                    return View(topSongs.Songs);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar top canciones: {ex.Message}";
                return View(new List<SongStatsDto>());
            }
        }

        // Métodos usando Crud para operaciones CRUD básicas cuando sea posible

        // GET: ArtistStatistics/GetAllSongs - Ejemplo usando Crud
        public async Task<IActionResult> GetAllSongs()
        {
            if (!await IsUserLoggedInAsync())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                Crud<SongStatsDto>.AuthToken = token;
                Crud<SongStatsDto>.EndPoint = $"{_apiBaseUrl}/artiststatistics/my-songs-stats";

                var songs = Crud<SongStatsDto>.GetAll();

                return Json(new { success = true, data = songs });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: ArtistStatistics/GetSongById/5 - Ejemplo usando Crud
        public async Task<IActionResult> GetSongById(int id)
        {
            if (!await IsUserLoggedInAsync())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");

                // Para este caso específico, usamos HttpClient porque el endpoint necesita formato específico
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    var response = await client.GetAsync($"{_apiBaseUrl}/artiststatistics/song-details/{id}");

                    if (!response.IsSuccessStatusCode)
                    {
                        return Json(new { success = false, message = $"Error: {response.StatusCode}" });
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<List<PlaybackStatisticDto>>(json);

                    return Json(new { success = true, data = data });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: ArtistStatistics/CreateSongStats - Ejemplo usando Crud Create
        [HttpPost]
        public async Task<IActionResult> CreateSongStats([FromBody] SongStatsDto songStats)
        {
            if (!await IsUserLoggedInAsync())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                Crud<SongStatsDto>.AuthToken = token;
                Crud<SongStatsDto>.EndPoint = $"{_apiBaseUrl}/songs"; // Endpoint para crear canciones

                var result = Crud<SongStatsDto>.Create(songStats);

                return Json(new { success = true, data = result, message = "Creado correctamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // PUT: ArtistStatistics/UpdateSongStats/5 - Ejemplo usando Crud Update
        [HttpPut]
        public async Task<IActionResult> UpdateSongStats(int id, [FromBody] SongStatsDto songStats)
        {
            if (!await IsUserLoggedInAsync())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                Crud<SongStatsDto>.AuthToken = token;
                Crud<SongStatsDto>.EndPoint = $"{_apiBaseUrl}/songs"; // Endpoint para actualizar canciones

                var result = Crud<SongStatsDto>.Update(id, songStats);

                return Json(new { success = result, message = result ? "Actualizado correctamente" : "Error al actualizar" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // DELETE: ArtistStatistics/DeleteSong/5 - Ejemplo usando Crud Delete
        [HttpDelete]
        public async Task<IActionResult> DeleteSong(int id)
        {
            if (!await IsUserLoggedInAsync())
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                Crud<SongStatsDto>.AuthToken = token;
                Crud<SongStatsDto>.EndPoint = $"{_apiBaseUrl}/songs"; // Endpoint para eliminar canciones

                var result = Crud<SongStatsDto>.Delete(id);

                return Json(new { success = result, message = result ? "Eliminado correctamente" : "Error al eliminar" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Método helper para usar Crud con diferentes endpoints
        private async Task<T> GetDataWithCrudAsync<T>(string endpoint, int? id = null)
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                throw new UnauthorizedAccessException("Token no encontrado");

            Crud<T>.AuthToken = token;
            Crud<T>.EndPoint = $"{_apiBaseUrl}/{endpoint}";

            if (id.HasValue)
                return Crud<T>.GetById(id.Value);
            else
            {
                var lista = Crud<T>.GetAll();
                return lista != null && lista.Any() ? lista.First() : default(T);
            }
        }

        // Método helper para obtener listas con Crud
        private async Task<List<T>> GetListWithCrudAsync<T>(string endpoint, string campo = null, int? valorCampo = null)
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                throw new UnauthorizedAccessException("Token no encontrado");

            Crud<T>.AuthToken = token;
            Crud<T>.EndPoint = $"{_apiBaseUrl}/{endpoint}";

            if (!string.IsNullOrEmpty(campo) && valorCampo.HasValue)
                return Crud<T>.GetBy(campo, valorCampo.Value);
            else
                return Crud<T>.GetAll();
        }

        // GET: ArtistStatistics/DownloadReport
        public async Task<IActionResult> DownloadReport()
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para descargar el reporte.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista para descargar el reporte.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    TempData["Error"] = "Token de sesión no encontrado.";
                    return RedirectToAction("Login", "Auth");
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    var response = await client.GetAsync($"{_apiBaseUrl}/artiststatistics/download-report");

                    if (!response.IsSuccessStatusCode)
                    {
                        TempData["Error"] = $"Error al generar el reporte: {response.StatusCode}";
                        return RedirectToAction(nameof(Index));
                    }

                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();

                    // Obtener el nombre del archivo de los headers de respuesta
                    var fileName = "Reporte_Estadisticas_Artista.pdf";
                    if (response.Content.Headers.ContentDisposition?.FileName != null)
                    {
                        fileName = response.Content.Headers.ContentDisposition.FileName.Trim('"');
                    }

                    return File(pdfBytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al descargar el reporte: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}