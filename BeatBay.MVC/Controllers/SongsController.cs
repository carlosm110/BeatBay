using Microsoft.AspNetCore.Mvc;
using BeatBay.DTOs;
using Newtonsoft.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace BeatBayMVC.Controllers
{
    public class SongsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public SongsController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiBaseUrl = configuration.GetValue<string>("ApiSettings:BaseUrl") ?? "https://localhost:7037/api";
        }

        private void AddAuthHeader()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }

        private async Task<bool> IsUserLoggedInAsync()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                AddAuthHeader();
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/auth/profile");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<UserDto?> GetCurrentUserAsync()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                AddAuthHeader();
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/auth/profile");
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<UserDto>(json);
            }
            catch
            {
                return null;
            }
        }

        private async Task<List<string>> GetUserRolesAsync()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return new List<string>();

            try
            {
                AddAuthHeader();
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

        // GET: Songs
        public async Task<IActionResult> Index()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/songs");
                if (!response.IsSuccessStatusCode)
                    return View(new List<SongDto>());

                var json = await response.Content.ReadAsStringAsync();
                var songs = JsonConvert.DeserializeObject<List<SongDto>>(json);

                // Verificar si el usuario está logueado para mostrar opciones adicionales
                ViewBag.IsLoggedIn = await IsUserLoggedInAsync();
                ViewBag.IsArtist = await UserHasRoleAsync("Artist");
                ViewBag.IsAdmin = await UserHasRoleAsync("Admin");

                return View(songs);
            }
            catch
            {
                return View(new List<SongDto>());
            }
        }

        // GET: Songs/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/songs/{id}");
                if (!response.IsSuccessStatusCode)
                    return NotFound();

                var json = await response.Content.ReadAsStringAsync();
                var song = JsonConvert.DeserializeObject<SongDto>(json);

                // Verificar permisos para editar/eliminar
                var currentUser = await GetCurrentUserAsync();
                ViewBag.CanEdit = false;
                ViewBag.CanDelete = false;

                if (currentUser != null)
                {
                    ViewBag.CanEdit = await UserHasRoleAsync("Admin") ||
                                     (await UserHasRoleAsync("Artist") && song.ArtistId == currentUser.Id);
                    ViewBag.CanDelete = ViewBag.CanEdit;
                }

                return View(song);
            }
            catch
            {
                return NotFound();
            }
        }

        // GET: Songs/Create
        public async Task<IActionResult> Create()
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para crear canciones.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista o administrador para crear canciones.";
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSongDto dto, IFormFile audioFile)
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para crear canciones.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista o administrador para crear canciones.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
                return View(dto);

            if (audioFile == null || audioFile.Length == 0)
            {
                ModelState.AddModelError("", "Debe seleccionar un archivo de audio.");
                return View(dto);
            }

            // Validar tamaño del archivo (máximo 50MB)
            if (audioFile.Length > 50 * 1024 * 1024)
            {
                ModelState.AddModelError("", "El archivo no puede ser mayor a 50MB.");
                return View(dto);
            }

            // Validar extensiones permitidas
            var allowedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a" };
            var fileExtension = Path.GetExtension(audioFile.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                ModelState.AddModelError("", "Formato de archivo no soportado. Solo se permiten: mp3, wav, flac, m4a");
                return View(dto);
            }

            try
            {
                AddAuthHeader();

                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(dto.Title), nameof(dto.Title));
                content.Add(new StringContent(dto.Duration.ToString()), nameof(dto.Duration));
                content.Add(new StringContent(dto.Genre ?? ""), nameof(dto.Genre));

                var fileContent = new StreamContent(audioFile.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(audioFile.ContentType);
                content.Add(fileContent, "audioFile", audioFile.FileName);

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/songs", content);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Canción creada exitosamente.";
                    return RedirectToAction(nameof(Index));
                }

                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError("", $"Error al crear la canción: {error}");
                return View(dto);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
                return View(dto);
            }
        }

        // GET: Songs/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para editar canciones.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista o administrador para editar canciones.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/songs/{id}");
                if (!response.IsSuccessStatusCode)
                    return NotFound();

                var json = await response.Content.ReadAsStringAsync();
                var song = JsonConvert.DeserializeObject<SongDto>(json);

                // Verificar permisos
                var currentUser = await GetCurrentUserAsync();
                var isAdmin = await UserHasRoleAsync("Admin");
                var isOwner = currentUser != null && song.ArtistId == currentUser.Id;

                if (!isAdmin && !isOwner)
                {
                    TempData["Error"] = "No tienes permisos para editar esta canción.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var updateDto = new UpdateSongDto
                {
                    Title = song.Title,
                    Duration = song.Duration,
                    Genre = song.Genre,
                    StreamingUrl = song.StreamingUrl,
                    IsActive = song.IsActive
                };

                ViewBag.SongId = id;
                ViewBag.SongTitle = song.Title;
                return View(updateDto);
            }
            catch
            {
                return NotFound();
            }
        }

        // POST: Songs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateSongDto dto)
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para editar canciones.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista o administrador para editar canciones.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                ViewBag.SongId = id;
                return View(dto);
            }

            try
            {
                AddAuthHeader();

                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"{_apiBaseUrl}/songs/{id}", content);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Canción actualizada exitosamente.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError("", $"Error al actualizar la canción: {error}");
                ViewBag.SongId = id;
                return View(dto);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
                ViewBag.SongId = id;
                return View(dto);
            }
        }

        // GET: Songs/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para eliminar canciones.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista o administrador para eliminar canciones.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/songs/{id}");
                if (!response.IsSuccessStatusCode)
                    return NotFound();

                var json = await response.Content.ReadAsStringAsync();
                var song = JsonConvert.DeserializeObject<SongDto>(json);

                // Verificar permisos
                var currentUser = await GetCurrentUserAsync();
                var isAdmin = await UserHasRoleAsync("Admin");
                var isOwner = currentUser != null && song.ArtistId == currentUser.Id;

                if (!isAdmin && !isOwner)
                {
                    TempData["Error"] = "No tienes permisos para eliminar esta canción.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                return View(song);
            }
            catch
            {
                return NotFound();
            }
        }

        // POST: Songs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para eliminar canciones.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista o administrador para eliminar canciones.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                AddAuthHeader();

                var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/songs/{id}");
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Canción eliminada correctamente.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Error al eliminar la canción: {error}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Songs/MySongs (Para que los artistas vean sus propias canciones)
        public async Task<IActionResult> MySongs()
        {
            if (!await IsUserLoggedInAsync())
            {
                TempData["Error"] = "Debes iniciar sesión para ver tus canciones.";
                return RedirectToAction("Login", "Auth");
            }

            if (!await UserHasRoleAsync("Artist") && !await UserHasRoleAsync("Admin"))
            {
                TempData["Error"] = "Debes ser artista para acceder a esta sección.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                AddAuthHeader();
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/songs/my-songs");

                if (!response.IsSuccessStatusCode)
                    return View(new List<SongDto>());

                var json = await response.Content.ReadAsStringAsync();
                var songs = JsonConvert.DeserializeObject<List<SongDto>>(json);

                ViewBag.IsMysongsPage = true;
                return View("Index", songs);
            }
            catch
            {
                return View("Index", new List<SongDto>());
            }
        }
    }
}