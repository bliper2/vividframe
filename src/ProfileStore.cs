using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;

namespace VividFrame
{
    /// <summary>
    /// Named graphics profiles stored as JSON under UserData\VividFrame\profiles.
    /// </summary>
    internal sealed class ProfileStore
    {
        private readonly MelonLogger.Instance _log;
        private readonly string _directory;

        public ProfileStore(MelonLogger.Instance log)
        {
            _log = log;
            string root = MelonEnvironment.UserDataDirectory;
            _directory = Path.Combine(root, "VividFrame", "profiles");
            try
            {
                Directory.CreateDirectory(_directory);
            }
            catch (Exception ex)
            {
                _log?.Error("Could not create the VividFrame profiles folder: {0}", ex);
            }
        }

        public string DirectoryPath => _directory;

        public string[] ListNames()
        {
            try
            {
                if (!Directory.Exists(_directory))
                    return Array.Empty<string>();
                string[] files = Directory.GetFiles(_directory, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                var names = new string[files.Length];
                for (int i = 0; i < files.Length; i++)
                    names[i] = Path.GetFileNameWithoutExtension(files[i]);
                return names;
            }
            catch (Exception ex)
            {
                _log?.Error("Could not list profiles: {0}", ex);
                return Array.Empty<string>();
            }
        }

        public bool Save(string name, Settings settings)
        {
            string safe = Sanitize(name);
            if (string.IsNullOrEmpty(safe) || settings == null)
                return false;
            try
            {
                Directory.CreateDirectory(_directory);
                string path = Path.Combine(_directory, safe + ".json");
                File.WriteAllText(path, ToJson(safe, settings.ExportValues()), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                _log?.Error("Could not save profile '{0}': {1}", name, ex);
                return false;
            }
        }

        public bool Load(string name, Settings settings)
        {
            string safe = Sanitize(name);
            if (string.IsNullOrEmpty(safe) || settings == null)
                return false;
            try
            {
                string path = Path.Combine(_directory, safe + ".json");
                if (!File.Exists(path))
                    return false;
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!TryParse(json, out _, out Dictionary<string, string> values))
                    return false;
                settings.ImportValues(values);
                return true;
            }
            catch (Exception ex)
            {
                _log?.Error("Could not load profile '{0}': {1}", name, ex);
                return false;
            }
        }

        public bool Delete(string name)
        {
            string safe = Sanitize(name);
            if (string.IsNullOrEmpty(safe))
                return false;
            try
            {
                string path = Path.Combine(_directory, safe + ".json");
                if (!File.Exists(path))
                    return false;
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                _log?.Error("Could not delete profile '{0}': {1}", name, ex);
                return false;
            }
        }

        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            var sb = new StringBuilder(name.Length);
            foreach (char c in name.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.')
                    sb.Append(c);
            }
            string cleaned = sb.ToString().Trim();
            if (cleaned.Length > 40)
                cleaned = cleaned.Substring(0, 40).Trim();
            return cleaned;
        }

        private static string ToJson(string name, Dictionary<string, string> values)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"name\": \"").Append(Escape(name)).Append("\",\n");
            sb.Append("  \"version\": \"").Append(Escape(VividFrameInfo.Version)).Append("\",\n");
            sb.Append("  \"values\": {\n");
            bool first = true;
            foreach (var pair in values)
            {
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append("    \"").Append(Escape(pair.Key)).Append("\": \"").Append(Escape(pair.Value ?? "")).Append("\"");
            }
            sb.Append("\n  }\n}\n");
            return sb.ToString();
        }

        private static bool TryParse(string json, out string name, out Dictionary<string, string> values)
        {
            name = null;
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(json))
                return false;
            name = ExtractString(json, "name");
            int start = json.IndexOf("\"values\"", StringComparison.Ordinal);
            if (start < 0)
                return false;
            int brace = json.IndexOf('{', start);
            if (brace < 0)
                return false;
            int i = brace + 1;
            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i >= json.Length || json[i] == '}')
                    break;
                if (json[i] != '"')
                    break;
                if (!TryReadString(json, ref i, out string key))
                    break;
                SkipWs(json, ref i);
                if (i >= json.Length || json[i] != ':')
                    break;
                i++;
                SkipWs(json, ref i);
                if (!TryReadString(json, ref i, out string value))
                    break;
                values[key] = value;
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                    i++;
            }
            return values.Count > 0;
        }

        private static string ExtractString(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int at = json.IndexOf(needle, StringComparison.Ordinal);
            if (at < 0)
                return null;
            int colon = json.IndexOf(':', at + needle.Length);
            if (colon < 0)
                return null;
            int i = colon + 1;
            SkipWs(json, ref i);
            return TryReadString(json, ref i, out string value) ? value : null;
        }

        private static bool TryReadString(string json, ref int i, out string value)
        {
            value = null;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '"')
                return false;
            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }
                if (c == '\\' && i < json.Length)
                {
                    char n = json[i++];
                    sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return false;
        }

        private static void SkipWs(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
