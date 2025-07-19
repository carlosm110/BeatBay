using BeatBay.APIConsumer;
using BeatBay.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BeatBay.MVC.Controllers
{
    public class PlaylistsController : Controller
    {
        private readonly ILogger<PlaylistsController> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _apiBaseUrl;

        public PlaylistsController(ILogger<PlaylistsController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _apiBaseUrl = configuration.GetValue<string>("ApiSettings:BaseUrl") ?? "https://localhost:7037/api";
        }

        // Método para configurar el endpoint y token antes de cada operación
        private void ConfigureCrud()
        {
            Crud<PlaylistDto>.EndPoint = $"{_apiBaseUrl}/playlists";
            var token = HttpContext.Session.GetString("JwtToken");
            Crud<PlaylistDto>.AuthToken = token;
        }

        private void ConfigureCrudForCreate()
        {
            Crud<CreatePlaylistDto>.EndPoint = $"{_apiBaseUrl}/playlists";
            var token = HttpContext.Session.GetString("JwtToken");
            Crud<CreatePlaylistDto>.AuthToken = token;
        }

        private void ConfigureCrudForSongs()
        {
            Crud<SongDto>.EndPoint = $"{_apiBaseUrl}/songs";
            var token = HttpContext.Session.GetString("JwtToken");
            Crud<SongDto>.AuthToken = token;
        }

        private void ConfigureCrudForAddSong()
        {
            Crud<AddSongToPlaylistDto>.EndPoint = $"{_apiBaseUrl}/playlists";
            var token = HttpContext.Session.GetString("JwtToken");
            Crud<AddSongToPlaylistDto>.AuthToken = token;
        }

        // Verificar si el usuario está autenticado
        private bool IsAuthenticated()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            return !string.IsNullOrEmpty(token);
        }

        // Método para verificar autenticación y redirigir si es necesario
        private IActionResult CheckAuthenticationAndRedirect()
        {
            if (!IsAuthenticated())
            {
                TempData["Error"] = "Debes iniciar sesión para acceder a esta función.";
                return RedirectToAction("Login", "Account");
            }
            return null;
        }

        // GET: Playlists - Públicas, sin autorización
        public async Task<IActionResult> Index()
        {
            try
            {
                ConfigureCrud();
                var playlists = Crud<PlaylistDto>.GetAll() ?? new List<PlaylistDto>();
                return View(playlists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando playlists");
                ViewBag.Error = "Error cargando playlists. Por favor intente nuevamente.";
                return View(new List<PlaylistDto>());
            }
        }

        // GET: Playlists/MyPlaylists - Requiere autenticación
        public async Task<IActionResult> MyPlaylists()
        {
            var authCheck = CheckAuthenticationAndRedirect();
            if (authCheck != null) return authCheck;

            try
            {
                ConfigureCrud();
                // Usar el endpoint específico para "my-playlists"
                Crud<PlaylistDto>.EndPoint = $"{_apiBaseUrl}/playlists/my-playlists";
                var playlists = Crud<PlaylistDto>.GetAll() ?? new List<PlaylistDto>();
                return View(playlists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando mis playlists");

                // Si es error de autorización, redirigir al login
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    TempData["Error"] = "Tu sesión ha expirado. Por favor inicia sesión nuevamente.";
                    return RedirectToAction("Login", "Account");
                }

                ViewBag.Error = "Error cargando sus playlists. Por favor intente nuevamente.";
                return View(new List<PlaylistDto>());
            }
        }

        // GET: Playlists/Details/5 - Público, pero con funcionalidad adicional si está autenticado
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                ConfigureCrud();
                var playlist = Crud<PlaylistDto>.GetById(id);

                if (playlist == null)
                    return NotFound();

                // Cargar canciones disponibles solo si el usuario está autenticado
                if (IsAuthenticated())
                {
                    try
                    {
                        ConfigureCrudForSongs();
                        var allSongs = Crud<SongDto>.GetAll() ?? new List<SongDto>();

                        // Filtrar solo canciones activas y que no estén en la playlist
                        var playlistSongIds = playlist.Songs.Select(s => s.Id).ToHashSet();
                        ViewBag.AvailableSongs = allSongs
                            .Where(s => s.IsActive && !playlistSongIds.Contains(s.Id))
                            .ToList();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error cargando canciones disponibles");
                        ViewBag.AvailableSongs = new List<SongDto>();
                    }
                }

                return View(playlist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando detalles de playlist");
                ViewBag.Error = "Error cargando detalles de la playlist.";
                return View();
            }
        }

        // GET: Playlists/Create - Requiere autenticación
        public async Task<IActionResult> Create()
        {
            var authCheck = CheckAuthenticationAndRedirect();
            if (authCheck != null) return authCheck;

            return View();
        }

        // POST: Playlists/Create - Requiere autenticación
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePlaylistDto dto)
        {
            var authCheck = CheckAuthenticationAndRedirect();
            if (authCheck != null) return authCheck;

            if (!ModelState.IsValid)
                return View(dto);

            try
            {
                ConfigureCrudForCreate();
                var createdPlaylist = Crud<CreatePlaylistDto>.Create(dto);

                TempData["Success"] = "Playlist creada exitosamente!";
                return RedirectToAction(nameof(MyPlaylists));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando playlist");

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    TempData["Error"] = "Tu sesión ha expirado. Por favor inicia sesión nuevamente.";
                    return RedirectToAction("Login", "Account");
                }

                ViewBag.Error = "Error creando playlist. " + ex.Message;
                return View(dto);
            }
        }

        // GET: Playlists/Edit/5 - Requiere autenticación y propiedad
        public async Task<IActionResult> Edit(int id)
        {
            var authCheck = CheckAuthenticationAndRedirect();
            if (authCheck != null) return authCheck;

            try
            {
                ConfigureCrud();
                var playlist = Crud<PlaylistDto>.GetById(id);

                if (playlist == null)
                {
                    TempData["Error"] = "Playlist no encontrada o no tienes permisos para editarla.";
                    return RedirectToAction(nameof(MyPlaylists));
                }

                var updateDto = new CreatePlaylistDto { Name = playlist.Name };
                ViewBag.PlaylistId = id;
                return View(updateDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando playlist para edición");

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    TempData["Error"] = "Tu sesión ha expirado. Por favor inicia sesión nuevamente.";
                    return RedirectToAction("Login", "Account");
                }

                TempData["Error"] = "Error cargando playlist para edición.";
                return RedirectToAction(nameof(MyPlaylists));
            }
        }

        // POST: Playlists/Edit/5 - Requiere autenticación y propiedad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CreatePlaylistDto dto)
        {
            var authCheck = CheckAuthenticationAndRedirect();
            if (authCheck != null) return authCheck;

            if (!ModelState.IsValid)
            {
                ViewBag.PlaylistId = id;
                return View(dto);
            }

            try
            {
                ConfigureCrudForCreate();
                var success = Crud<CreatePlaylistDto>.Update(id, dto);

                if (success)
                {
                    TempData["Success"] = "Playlist actualizada exitosamente!";
                    return RedirectToAction(nameof(MyPlaylists));
                }

                ViewBag.Error = "Error actualizando playlist.";
                ViewBag.PlaylistId = id;
                return View(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando playlist");

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    TempData["Error"] = "Tu sesión ha expirado. Por favor inicia sesión nuevamente.";
                    return RedirectToAction("Login", "Account");
                }

                if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
                {
                    TempData["Error"] = "No tienes permisos para editar esta playlist.";
                    return RedirectToAction(nameof(MyPlaylists));
                }

                if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    TempData["Error"] = "La playlist no fue encontrada.";
                    return RedirectToAction(nameof(MyPlaylists));
                }

                ViewBag.Error = "Error actualizando playlist. " + ex.Message;
                ViewBag.PlaylistId = id;
                return View(dto);
            }
        }

        // GET: Playlists/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var authCheck = CheckAuthenticationAndRedirect();
            if (authCheck != null) return authCheck;

            try
            {
                ConfigureCrud();
                var playlist = Crud<PlaylistDto>.GetById(id);

                if (playlist == null)
                {
                    TempData["Error"] = "Playlist no encontrada.";
                    return RedirectToAction(nameof(MyPlaylists));
                }

                return View(playlist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando playlist para eliminación");

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    TempData["Error"] = "Tu sesión ha expirado. Por favor inicia sesión nuevamente.";
                    return RedirectToAction("Login", "Account");
                }

                TempData["Error"] = "Error cargando playlist.";
                return RedirectToAction(nameof(MyPlaylists));
            }
        }

        // POST: Playlists/DeleteConfirmed/5 - Requiere autenticación y propiedad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var authCheck = CheckAuthenticationAndRedirect();
            if (authCheck != null) return authCheck;

            try
            {
                ConfigureCrud();
                var success = Crud<PlaylistDto>.Delete(id);

                if (success)
                {
                    TempData["Success"] = "Playlist eliminada exitosamente!";
                }
                else
                {
                    TempData["Error"] = "Error eliminando playlist.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando playlist");

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    TempData["Error"] = "Tu sesión ha expirado. Por favor inicia sesión nuevamente.";
                    return RedirectToAction("Login", "Account");
                }

                if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
                {
                    TempData["Error"] = "No tienes permisos para eliminar esta playlist.";
                }
                else if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    TempData["Error"] = "La playlist no fue encontrada.";
                }
                else
                {
                    TempData["Error"] = "Error eliminando playlist. " + ex.Message;
                }
            }

            return RedirectToAction(nameof(MyPlaylists));
        }

        // POST: Playlists/AddSong/5 - Requiere autenticación y propiedad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSong(int id, AddSongToPlaylistDto dto)
        {
            var authCheck = CheckAuthenticationAndRedirect();
            if (authCheck != null) return authCheck;

            try
            {
                ConfigureCrudForAddSong();
                // Configurar endpoint específico para agregar canción
                Crud<AddSongToPlaylistDto>.EndPoint = $"{_apiBaseUrl}/playlists/{id}/songs";

                var result = Crud<AddSongToPlaylistDto>.Create(dto);
                TempData["Success"] = "Canción agregada a la playlist exitosamente!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error agregando canción a playlist");

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    TempData["Error"] = "Tu sesión ha expirado. Por favor inicia sesión nuevamente.";
                    return RedirectToAction("Login", "Account");
                }

                if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
                {
                    TempData["Error"] = "No tienes permisos para modificar esta playlist.";
                }
                else if (ex.Message.Contains("400") || ex.Message.Contains("Bad Request"))
                {
                    TempData["Error"] = "La canción ya está en la playlist o no es válida.";
                }
                else
                {
                    TempData["Error"] = "Error agregando canción a la playlist. " + ex.Message;
                }
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Playlists/RemoveSong/5 - Requiere autenticación y propiedad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSong(int id, int songId)
        {
            var authCheck = CheckAuthenticationAndRedirect();
            if (authCheck != null) return authCheck;

            try
            {
                // Para eliminar canción, necesitamos usar un endpoint específico
                // Como la clase Crud no tiene un método para endpoints complejos,
                // podemos usar el patrón de configurar el endpoint completo
                Crud<object>.EndPoint = $"{_apiBaseUrl}/playlists/{id}/songs";
                var token = HttpContext.Session.GetString("JwtToken");
                Crud<object>.AuthToken = token;

                var success = Crud<object>.Delete(songId);

                if (success)
                {
                    TempData["Success"] = "Canción removida de la playlist exitosamente!";
                }
                else
                {
                    TempData["Error"] = "Error removiendo canción de la playlist.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removiendo canción de playlist");

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    TempData["Error"] = "Tu sesión ha expirado. Por favor inicia sesión nuevamente.";
                    return RedirectToAction("Login", "Account");
                }

                if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
                {
                    TempData["Error"] = "No tienes permisos para modificar esta playlist.";
                }
                else if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    TempData["Error"] = "La canción no está en la playlist.";
                }
                else
                {
                    TempData["Error"] = "Error removiendo canción de la playlist. " + ex.Message;
                }
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}