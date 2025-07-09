namespace BeatBay.API.Services
{
    public class LightThemeStrategy : IThemeStrategy
    {
        public string GetTheme()
        {
            return "light";  // El valor representará el modo claro
        }
    }
}
