using Azure.Storage.Blobs;
using BeatBay.DTOs;
using BeatBay.Data;
using BeatBay.Model;
using BeatBay.API.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BeatBay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SongsController : ControllerBase
    {
        private readonly BeatBayDbContext _context;
        private readonly BlobContainerClient _blobContainerClient;
        private readonly AzureBlobStorageSettings _blobSettings;
        private readonly UserManager<User> _userManager;

        public SongsController(BeatBayDbContext context, IOptions<AzureBlobStorageSettings> blobSettings, UserManager<User> userManager)
        {
            _context = context;
            _blobSettings = blobSettings.Value;
            _userManager = userManager;

            // Crear BlobContainerClient usando la configuración del JSON
            _blobContainerClient = new BlobContainerClient(new Uri(_blobSettings.ContainerUrl));
        }

        // Método helper para verificar roles usando Identity
        private async Task<bool> IsUserInRoleAsync(int userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || !user.IsActive)
                return false;

            return await _userManager.IsInRoleAsync(user, role);
        }

        // Método helper para obtener el usuario actual
        private async Task<User?> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return null;

            return await _userManager.FindByIdAsync(userId.ToString());
        }

        // GET: api/songs?isActive=true
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SongDto>>> GetSongs([FromQuery] bool? isActive = null)
        {
            var query = _context.Songs.Include(s => s.Artist).AsQueryable();

            if (isActive.HasValue)
                query = query.Where(s => s.IsActive == isActive.Value);

            var songs = await query
                .Select(s => new SongDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Duration = s.Duration,
                    Genre = s.Genre,
                    StreamingUrl = s.StreamingUrl,
                    ArtistId = s.ArtistId,
                    ArtistName = s.Artist.Name ?? s.Artist.UserName,
                    IsActive = s.IsActive,
                    UploadedAt = s.UploadedAt,
                    PlayCount = s.PlaybackStatistics.Sum(ps => ps.PlayCount)
                })
                .ToListAsync();

            return Ok(songs);
        }

        // GET: api/songs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SongDto>> GetSong(int id)
        {
            var song = await _context.Songs
                .Include(s => s.Artist)
                .Include(s => s.PlaybackStatistics)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (song == null)
                return NotFound();

            var songDto = new SongDto
            {
                Id = song.Id,
                Title = song.Title,
                Duration = song.Duration,
                Genre = song.Genre,
                StreamingUrl = song.StreamingUrl,
                ArtistId = song.ArtistId,
                ArtistName = song.Artist.Name ?? song.Artist.UserName,
                IsActive = song.IsActive,
                UploadedAt = song.UploadedAt,
                PlayCount = song.PlaybackStatistics.Sum(ps => ps.PlayCount)
            };

            return Ok(songDto);
        }

        // POST: api/songs
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<SongDto>> CreateSong([FromForm] CreateSongDto dto, IFormFile audioFile)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            // Verificar si el usuario es Artist o Admin usando Identity
            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");
            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (!isArtist && !isAdmin)
                return Forbid("Debes ser artista o administrador para crear canciones.");

            if (audioFile == null || audioFile.Length == 0)
                return BadRequest("No se ha seleccionado un archivo de audio.");

            var allowedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a" };
            var fileExtension = Path.GetExtension(audioFile.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest("Formato de archivo no soportado. Solo se permiten: mp3, wav, flac, m4a");

            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

            try
            {
                // Subir archivo a Azure Blob Storage
                var blobClient = _blobContainerClient.GetBlobClient(uniqueFileName);
                using var stream = audioFile.OpenReadStream();
                await blobClient.UploadAsync(stream, overwrite: true);

                var streamingUrl = blobClient.Uri.ToString();

                var song = new Song
                {
                    Title = dto.Title,
                    Duration = dto.Duration,
                    Genre = dto.Genre,
                    StreamingUrl = streamingUrl,
                    ArtistId = currentUser.Id,
                    IsActive = true,
                    UploadedAt = DateTime.UtcNow
                };

                _context.Songs.Add(song);
                await _context.SaveChangesAsync();

                var songDto = new SongDto
                {
                    Id = song.Id,
                    Title = song.Title,
                    Duration = song.Duration,
                    Genre = song.Genre,
                    StreamingUrl = song.StreamingUrl,
                    ArtistId = song.ArtistId,
                    ArtistName = currentUser.Name ?? currentUser.UserName,
                    IsActive = song.IsActive,
                    UploadedAt = song.UploadedAt,
                    PlayCount = 0
                };

                return CreatedAtAction(nameof(GetSong), new { id = song.Id }, songDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al subir el archivo: {ex.Message}");
            }
        }

        // PUT: api/songs/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateSong(int id, UpdateSongDto dto)
        {
            var song = await _context.Songs.FindAsync(id);
            if (song == null)
                return NotFound();

            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");
            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");

            // Verificar si el usuario tiene permisos para editar
            if (!isArtist && !isAdmin)
                return Forbid("Debes ser artista o administrador para editar canciones.");

            // Solo el artista dueño de la canción o un admin puede editarla
            if (song.ArtistId != currentUser.Id && !isAdmin)
                return Forbid("No tienes permisos para editar esta canción.");

            if (!string.IsNullOrWhiteSpace(dto.Title))
                song.Title = dto.Title;

            if (dto.Duration.HasValue)
                song.Duration = dto.Duration.Value;

            if (!string.IsNullOrWhiteSpace(dto.Genre))
                song.Genre = dto.Genre;

            if (!string.IsNullOrWhiteSpace(dto.StreamingUrl))
                song.StreamingUrl = dto.StreamingUrl;

            if (dto.IsActive.HasValue)
                song.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            // Log si es un admin editando canción de otro usuario
            if (isAdmin && song.ArtistId != currentUser.Id)
            {
                var log = new AdminActionLog
                {
                    AdminUserId = currentUser.Id,
                    ActionType = "Edit Song",
                    Description = $"Edited song: {song.Title} (ID: {song.Id})"
                };
                _context.AdminActionLogs.Add(log);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Canción actualizada correctamente" });
        }

        // DELETE: api/songs/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteSong(int id)
        {
            var song = await _context.Songs.FindAsync(id);
            if (song == null)
                return NotFound();

            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");
            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");

            // Verificar si el usuario tiene permisos para eliminar
            if (!isArtist && !isAdmin)
                return Forbid("Debes ser artista o administrador para eliminar canciones.");

            // Solo el artista dueño de la canción o un admin puede eliminarla
            if (song.ArtistId != currentUser.Id && !isAdmin)
                return Forbid("No tienes permisos para eliminar esta canción.");

            song.IsActive = false;
            await _context.SaveChangesAsync();

            // Log si es un admin eliminando canción de otro usuario
            if (isAdmin && song.ArtistId != currentUser.Id)
            {
                var log = new AdminActionLog
                {
                    AdminUserId = currentUser.Id,
                    ActionType = "Delete Song",
                    Description = $"Deactivated song: {song.Title} (ID: {song.Id})"
                };
                _context.AdminActionLogs.Add(log);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Canción desactivada correctamente" });
        }

        // GET: api/songs/my-songs (Para artistas ver sus propias canciones)
        [HttpGet("my-songs")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<SongDto>>> GetMySongs()
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            // Verificar si el usuario es Artist o Admin usando Identity
            var isArtist = await IsUserInRoleAsync(currentUser.Id, "Artist");
            var isAdmin = await IsUserInRoleAsync(currentUser.Id, "Admin");

            if (!isArtist && !isAdmin)
                return Forbid("Debes ser artista o administrador para ver tus canciones.");

            var songs = await _context.Songs
                .Include(s => s.Artist)
                .Include(s => s.PlaybackStatistics)
                .Where(s => s.ArtistId == currentUser.Id)
                .Select(s => new SongDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Duration = s.Duration,
                    Genre = s.Genre,
                    StreamingUrl = s.StreamingUrl,
                    ArtistId = s.ArtistId,
                    ArtistName = s.Artist.Name ?? s.Artist.UserName,
                    IsActive = s.IsActive,
                    UploadedAt = s.UploadedAt,
                    PlayCount = s.PlaybackStatistics.Sum(ps => ps.PlayCount)
                })
                .ToListAsync();

            return Ok(songs);
        }

        // GET: api/songs/user-roles (Para que el MVC pueda obtener los roles del usuario)
        [HttpGet("user-roles")]
        [Authorize]
        public async Task<ActionResult<List<string>>> GetUserRoles()
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
                return Unauthorized("Usuario no encontrado.");

            var roles = await _userManager.GetRolesAsync(currentUser);
            return Ok(roles.ToList());
        }
    }
}