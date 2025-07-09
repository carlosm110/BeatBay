using BeatBay.API.Services;
using BeatBay.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeatBay.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ThemeController : ControllerBase
    {
        private readonly ThemeContext _themeContext;

        public ThemeController(ThemeContext themeContext)
        {
            _themeContext = themeContext;
        }

        // Endpoint para obtener el tema actual (modo claro u oscuro)
        [HttpGet]
        public string Get()
        {
            return _themeContext.GetCurrentTheme();  // Retorna el tema actual
        }
    }
}
