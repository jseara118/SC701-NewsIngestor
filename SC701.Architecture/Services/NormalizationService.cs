using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SC701.Models.DTOs;

namespace SC701.Architecture.Services
{
    /// <summary>
    /// Servicio para normalizar contenido crudo al formato estándar acordado
    /// HU-18: Normalizar contenido al formato acordado
    /// </summary>
    public class NormalizationService : INormalizationService
    {
        public StandardNewsItemDto NormalizeFromJson(string jsonContent, string sourceId, string sourceName, string sourceUrl, string sourceType)
        {
            try
            {
                // Intentar deserializar como StandardNewsItemDto
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var standardItem = JsonSerializer.Deserialize<StandardNewsItemDto>(jsonContent, options);
                if (standardItem != null && !string.IsNullOrWhiteSpace(standardItem.Normalized?.Title))
                {
                    // Ya está en formato estándar, solo actualizar source si es necesario
                    if (standardItem.Source == null)
                    {
                        standardItem.Source = new SourceDto
                        {
                            Id = sourceId,
                            Name = sourceName,
                            Type = sourceType,
                            Url = sourceUrl,
                            RequiresSecret = false
                        };
                    }
                    return standardItem;
                }
            }
            catch
            {
                // No está en formato estándar, continuar con normalización
            }

            // Normalizar desde JSON genérico
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            var title = ExtractJsonValue(jsonElement, "title", "headline", "name");
            var content = ExtractJsonValue(jsonElement, "content", "body", "description", "text");
            var publishedAt = ExtractJsonDate(jsonElement, "publishedAt", "published", "date", "createdAt");

            return CreateStandardItem(
                sourceId,
                sourceName,
                sourceUrl,
                sourceType,
                title ?? "Sin título",
                content ?? "",
                publishedAt,
                jsonContent,
                "json"
            );
        }

        public StandardNewsItemDto NormalizeFromXml(string xmlContent, string sourceId, string sourceName, string sourceUrl, string sourceType)
        {
            try
            {
                var xmlDoc = XDocument.Parse(xmlContent);
                var root = xmlDoc.Root;

                if (root == null)
                {
                    return CreateStandardItem(sourceId, sourceName, sourceUrl, sourceType, "Sin título", xmlContent, DateTime.UtcNow, xmlContent, "xml");
                }

                var title = root.Element("title")?.Value ??
                           root.Element("headline")?.Value ?? "Sin título";
                var content = root.Element("content")?.Value ??
                          root.Element("body")?.Value ??
                          root.Element("description")?.Value ?? "";
                var publishedAt = ExtractXmlDate(root, "publishedAt", "published", "date");

                return CreateStandardItem(
                    sourceId,
                    sourceName,
                    sourceUrl,
                    sourceType,
                    title,
                    content,
                    publishedAt,
                    xmlContent,
                    "xml"
                );
            }
            catch
            {
                return CreateStandardItem(sourceId, sourceName, sourceUrl, sourceType, "Sin título", xmlContent, DateTime.UtcNow, xmlContent, "xml");
            }
        }

        public StandardNewsItemDto NormalizeFromHtml(string htmlContent, string sourceId, string sourceName, string sourceUrl, string sourceType)
        {
            // Extraer título del HTML
            var titleMatch = Regex.Match(htmlContent, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var title = titleMatch.Success ? StripHtmlTags(titleMatch.Groups[1].Value) : "Sin título";

            // Extraer contenido principal (remover scripts, styles, etc.)
            var bodyMatch = Regex.Match(htmlContent, @"<body[^>]*>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var bodyContent = bodyMatch.Success ? bodyMatch.Groups[1].Value : htmlContent;

            // Remover scripts y styles
            bodyContent = Regex.Replace(bodyContent, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            bodyContent = Regex.Replace(bodyContent, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var content = StripHtmlTags(bodyContent);
            if (content.Length > 5000)
            {
                content = content.Substring(0, 5000) + "...";
            }

            return CreateStandardItem(
                sourceId,
                sourceName,
                sourceUrl,
                sourceType,
                title,
                content,
                DateTime.UtcNow,
                htmlContent,
                "html"
            );
        }

        private StandardNewsItemDto CreateStandardItem(
            string sourceId,
            string sourceName,
            string sourceUrl,
            string sourceType,
            string title,
            string content,
            DateTime publishedAt,
            string rawContent,
            string rawFormat)
        {
            return new StandardNewsItemDto
            {
                SchemaVersion = "edu.univ.ingest.v1",
                ExportedAt = DateTime.UtcNow,
                Source = new SourceDto
                {
                    Id = sourceId,
                    Name = sourceName,
                    Type = sourceType,
                    Url = sourceUrl,
                    RequiresSecret = false
                },
                Normalized = new NormalizedContentDto
                {
                    Id = Guid.NewGuid().ToString(),
                    ExternalId = null,
                    Title = title,
                    Content = content,
                    Summary = content.Length > 200 ? content.Substring(0, 200) + "..." : content,
                    PublishedAt = publishedAt,
                    Url = sourceUrl,
                    Author = null,
                    Language = "es",
                    Category = null
                },
                Raw = new RawDataDto
                {
                    Format = rawFormat,
                    Data = new { original = rawContent }
                }
            };
        }

        // Helper: busca propiedad en JsonElement de forma case-insensitive
        private bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement prop)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out prop))
            {
                return true;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in element.EnumerateObject())
                {
                    if (string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        prop = p.Value;
                        return true;
                    }
                }
            }

            prop = default;
            return false;
        }

        private string? ExtractJsonValue(JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (TryGetPropertyCaseInsensitive(element, key, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.String)
                    {
                        return prop.GetString();
                    }
                    if (prop.ValueKind == JsonValueKind.Number)
                    {
                        return prop.GetRawText();
                    }
                    // Si es objeto/array, devolver representación raw como fallback
                    return prop.GetRawText();
                }
            }
            return null;
        }

        private DateTime ExtractJsonDate(JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (TryGetPropertyCaseInsensitive(element, key, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.String)
                    {
                        var s = prop.GetString();
                        if (DateTime.TryParse(s, out var date))
                        {
                            return date;
                        }
                        if (long.TryParse(s, out var longTs))
                        {
                            try
                            {
                                return DateTimeOffset.FromUnixTimeSeconds(longTs).DateTime;
                            }
                            catch { }
                        }
                    }
                    else if (prop.ValueKind == JsonValueKind.Number)
                    {
                        if (prop.TryGetInt64(out var timestamp))
                        {
                            try
                            {
                                return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                            }
                            catch { }
                        }
                        // Fallback: intentar parsear raw
                        var raw = prop.GetRawText();
                        if (long.TryParse(raw, out var ts2))
                        {
                            try
                            {
                                return DateTimeOffset.FromUnixTimeSeconds(ts2).DateTime;
                            }
                            catch { }
                        }
                    }
                }
            }
            return DateTime.UtcNow;
        }

        private DateTime ExtractXmlDate(XElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                var dateElement = element.Element(key);
                if (dateElement != null && DateTime.TryParse(dateElement.Value, out var date))
                {
                    return date;
                }
            }
            return DateTime.UtcNow;
        }

        private string StripHtmlTags(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return "";

            var text = Regex.Replace(html, "<.*?>", " ");
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }
    }
}

