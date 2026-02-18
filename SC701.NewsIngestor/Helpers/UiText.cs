namespace SC701.NewsIngestor.Helpers
{
    public static class UiText
    {
        public static string T(string key, string lang)
        {
            lang = (lang ?? "es").ToLower();

            return key switch
            {
                // Navbar
                "nav.home" => lang == "en" ? "Home" : "Inicio",
                "nav.sources" => lang == "en" ? "Sources" : "Fuentes",
                "nav.items" => lang == "en" ? "Items" : "Ítems",
                "nav.settings" => lang == "en" ? "Settings" : "Configuración",
                "nav.prefs" => lang == "en" ? "Preferences" : "Preferencias",
                "nav.secrets" => lang == "en" ? "Secrets" : "Secrets",
                "nav.users_roles" => lang == "en" ? "Users & Roles" : "Usuarios & Roles",
                "nav.logout" => lang == "en" ? "Logout" : "Salir",
                "nav.login" => lang == "en" ? "Login" : "Ingresar",

                _ => key
            };
        }
    }
}
