namespace BeatBay.API.Services
{
    public class ThemeContext
    {
        private readonly IThemeStrategy _themeStrategy;

        public ThemeContext()
        {
            var currentHour = DateTime.Now.Hour;

            // Si es de noche (18:00 - 06:00), aplica el tema oscuro
            if (currentHour >= 18 || currentHour < 6)
            {
                _themeStrategy = new DarkThemeStrategy();
            }
            else
            {
                _themeStrategy = new LightThemeStrategy();
            }
        }

        public string GetCurrentTheme()
        {
            return _themeStrategy.GetTheme();
        }
    }

}
