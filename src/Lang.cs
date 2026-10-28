using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace Sidekey
{
    // Переводы интерфейса. Тексты лежат в lang\<код>.json и встраиваются в .exe при сборке.
    // Если строки нет в выбранном языке, берётся английская.
    static class L
    {
        // Код, название на самом языке, культура для дат и дней недели.
        public static readonly string[,] Languages =
        {
            { "en", "English", "en-US" },
            { "zh", "中文（简体）", "zh-CN" },
            { "hi", "हिन्दी", "hi-IN" },
            { "es", "Español", "es-ES" },
            { "fr", "Français", "fr-FR" },
            { "ar", "العربية", "ar-EG" },
            { "bn", "বাংলা", "bn-BD" },
            { "pt", "Português", "pt-BR" },
            { "ru", "Русский", "ru-RU" },
            { "de", "Deutsch", "de-DE" },
        };

        public static string Code = "en";
        public static CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");
        static Dictionary<string, string> current = new Dictionary<string, string>(), fallback = new Dictionary<string, string>();

        public static int Count { get { return Languages.GetLength(0); } }

        public static int IndexOf(string code)
        {
            for (int i = 0; i < Count; i++) if (Languages[i, 0] == code) return i;
            return -1;
        }

        public static void Load(string code)
        {
            int i = IndexOf(code);
            if (i < 0) { code = "en"; i = 0; }
            Code = code;
            try { Culture = CultureInfo.GetCultureInfo(Languages[i, 2]); }
            catch { Culture = CultureInfo.InvariantCulture; }
            fallback = Read("en");
            current = code == "en" ? fallback : Read(code);
        }

        public static Dictionary<string, string> Read(string code)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Sidekey.lang." + code + ".json"))
            {
                if (stream == null) return new Dictionary<string, string>();
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(reader.ReadToEnd()) ?? new Dictionary<string, string>();
            }
        }

        public static string T(string key)
        {
            string v;
            if (current.TryGetValue(key, out v) || fallback.TryGetValue(key, out v)) return v;
            return key;
        }

        public static string F(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch (FormatException) { return T(key); }
        }
    }
}
