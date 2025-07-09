namespace BeatBay.API.Services
{
    public class DarkThemeStrategy : IThemeStrategy
    {
        public string GetTheme()
        {
            return "dark";  // El valor representará el modo oscuro
        }
    }

}
