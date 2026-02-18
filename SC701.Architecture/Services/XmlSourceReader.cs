using System.Net.Http;
using System.Xml.Linq;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.Architecture.Services
{
    /// <summary>
    /// Implementación para leer fuentes XML (RSS, Atom, etc.)
    /// HU-18: Normalizar contenido al formato acordado
    /// </summary>
    public class XmlSourceReader : ISourceReader
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly INormalizationService _normalizationService;

        public XmlSourceReader(IHttpClientFactory httpClientFactory, INormalizationService normalizationService)
        {
            _httpClientFactory = httpClientFactory;
            _normalizationService = normalizationService;
        }

        public bool CanHandle(string componentType)
        {
            return componentType.Equals("xml", StringComparison.OrdinalIgnoreCase) ||
                   componentType.Equals("rss", StringComparison.OrdinalIgnoreCase) ||
                   componentType.Equals("atom", StringComparison.OrdinalIgnoreCase) ||
                   componentType.Equals("feed", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<StandardNewsItemDto>> ReadFromSourceAsync(Source source, string? secretValue = null)
        {
            var items = new List<StandardNewsItemDto>();

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, source.Url);

                if (source.RequiresSecret && !string.IsNullOrWhiteSpace(secretValue))
                {
                    request.Headers.Add("Authorization", $"Bearer {secretValue}");
                }

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var xmlContent = await response.Content.ReadAsStringAsync();
                var xmlDoc = XDocument.Parse(xmlContent);

                // Procesar RSS
                if (xmlDoc.Root?.Name.LocalName == "rss" || xmlDoc.Root?.Name.LocalName == "RDF")
                {
                    var channel = xmlDoc.Root.Element("channel");
                    if (channel != null)
                    {
                        var entries = channel.Elements("item");
                        foreach (var entry in entries)
                        {
                            var normalized = NormalizeRssItem(entry, source);
                            items.Add(normalized);
                        }
                    }
                }
                // Procesar Atom
                else if (xmlDoc.Root != null && (xmlDoc.Root.Name.LocalName == "feed" || (xmlDoc.Root.Name.NamespaceName?.Contains("atom") ?? false)))
                {
                    var entries = xmlDoc.Root.Elements().Where(e => e.Name.LocalName == "entry");
                    foreach (var entry in entries)
                    {
                        var normalized = NormalizeAtomItem(entry, source);
                        items.Add(normalized);
                    }
                }
                else
                {
                    // XML genérico - normalizar como un solo item
                    var normalized = _normalizationService.NormalizeFromXml(
                        xmlContent,
                        source.Id.ToString(),
                        source.Name,
                        source.Url,
                        source.ComponentType
                    );
                    items.Add(normalized);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error leyendo fuente XML {source.Name}: {ex.Message}");
            }

            return items;
        }

        private StandardNewsItemDto NormalizeRssItem(XElement item, Source source)
        {
            var title = item.Element("title")?.Value ?? "Sin título";
            var description = item.Element("description")?.Value ?? "";
            var link = item.Element("link")?.Value ?? source.Url;
            var pubDate = item.Element("pubDate")?.Value ?? DateTime.UtcNow.ToString("R");

            DateTime publishedAt;
            if (!DateTime.TryParse(pubDate, out publishedAt))
            {
                publishedAt = DateTime.UtcNow;
            }

            var author = item.Element("author")?.Value ?? item.Element("dc:creator")?.Value;

            return new StandardNewsItemDto
            {
                SchemaVersion = "edu.univ.ingest.v1",
                ExportedAt = DateTime.UtcNow,
                Source = new SourceDto
                {
                    Id = source.Id.ToString(),
                    Name = source.Name,
                    Type = source.ComponentType,
                    Url = source.Url,
                    RequiresSecret = source.RequiresSecret
                },
                Normalized = new NormalizedContentDto
                {
                    Id = Guid.NewGuid().ToString(),
                    ExternalId = item.Element("guid")?.Value ?? link,
                    Title = title,
                    Content = description,
                    Summary = description.Length > 200 ? description.Substring(0, 200) + "..." : description,
                    PublishedAt = publishedAt,
                    Url = link,
                    Author = author,
                    Language = "es",
                    Category = null
                },
                Raw = new RawDataDto
                {
                    Format = "xml",
                    Data = new { original = item.ToString() }
                }
            };
        }

        private StandardNewsItemDto NormalizeAtomItem(XElement entry, Source source)
        {
            var title = entry.Element("title")?.Value ?? "Sin título";
            var summary = entry.Element("summary")?.Value ?? "";
            var content = entry.Element("content")?.Value ?? summary;
            var link = entry.Elements("link")
                .FirstOrDefault(l => l.Attribute("rel")?.Value != "self")?.Attribute("href")?.Value
                ?? source.Url;
            var published = entry.Element("published")?.Value ?? entry.Element("updated")?.Value;

            DateTime publishedAt;
            if (!DateTime.TryParse(published, out publishedAt))
            {
                publishedAt = DateTime.UtcNow;
            }

            var author = entry.Element("author")?.Element("name")?.Value;

            return new StandardNewsItemDto
            {
                SchemaVersion = "edu.univ.ingest.v1",
                ExportedAt = DateTime.UtcNow,
                Source = new SourceDto
                {
                    Id = source.Id.ToString(),
                    Name = source.Name,
                    Type = source.ComponentType,
                    Url = source.Url,
                    RequiresSecret = source.RequiresSecret
                },
                Normalized = new NormalizedContentDto
                {
                    Id = entry.Element("id")?.Value ?? Guid.NewGuid().ToString(),
                    ExternalId = entry.Element("id")?.Value,
                    Title = title,
                    Content = content,
                    Summary = summary,
                    PublishedAt = publishedAt,
                    Url = link,
                    Author = author,
                    Language = "es",
                    Category = null
                },
                Raw = new RawDataDto
                {
                    Format = "xml",
                    Data = new { original = entry.ToString() }
                }
            };
        }
    }
}

