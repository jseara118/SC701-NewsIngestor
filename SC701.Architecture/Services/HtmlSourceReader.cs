using System.Net.Http;
using System.Text.RegularExpressions;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.Architecture.Services
{
    /// <summary>
    /// Implementación para leer fuentes HTML (web scraping básico)
    /// HU-18: Normalizar contenido al formato acordado
    /// </summary>
    public class HtmlSourceReader : ISourceReader
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly INormalizationService _normalizationService;

        public HtmlSourceReader(IHttpClientFactory httpClientFactory, INormalizationService normalizationService)
        {
            _httpClientFactory = httpClientFactory;
            _normalizationService = normalizationService;
        }

        public bool CanHandle(string componentType)
        {
            return componentType.Equals("html", StringComparison.OrdinalIgnoreCase) ||
                   componentType.Equals("web", StringComparison.OrdinalIgnoreCase) ||
                   componentType.Equals("scraping", StringComparison.OrdinalIgnoreCase);
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

                // Agregar User-Agent para evitar bloqueos
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var htmlContent = await response.Content.ReadAsStringAsync();
                
                // Extraer artículos del HTML (búsqueda básica de elementos article, h1-h6, etc.)
                var articles = ExtractArticlesFromHtml(htmlContent, source);
                
                if (articles.Any())
                {
                    items.AddRange(articles);
                }
                else
                {
                    // Si no se encuentran artículos estructurados, normalizar todo el HTML
                    var normalized = _normalizationService.NormalizeFromHtml(
                        htmlContent,
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
                Console.WriteLine($"Error leyendo fuente HTML {source.Name}: {ex.Message}");
            }

            return items;
        }

        private List<StandardNewsItemDto> ExtractArticlesFromHtml(string html, Source source)
        {
            var items = new List<StandardNewsItemDto>();
            
            // Patrón básico para encontrar elementos article
            var articlePattern = @"<article[^>]*>(.*?)</article>";
            var matches = Regex.Matches(html, articlePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            
            foreach (Match match in matches)
            {
                var articleHtml = match.Groups[1].Value;
                var title = ExtractTitle(articleHtml);
                var content = StripHtmlTags(articleHtml);
                
                if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(content))
                {
                    items.Add(new StandardNewsItemDto
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
                            ExternalId = null,
                            Title = title ?? "Sin título",
                            Content = content ?? "",
                            Summary = content?.Length > 200 ? content.Substring(0, 200) + "..." : content,
                            PublishedAt = DateTime.UtcNow,
                            Url = source.Url,
                            Author = null,
                            Language = "es",
                            Category = null
                        },
                        Raw = new RawDataDto
                        {
                            Format = "html",
                            Data = new { original = articleHtml }
                        }
                    });
                }
            }
            
            return items;
        }

        private string? ExtractTitle(string html)
        {
            // Buscar h1, h2, h3 dentro del artículo
            var titlePattern = @"<h[1-3][^>]*>(.*?)</h[1-3]>";
            var match = Regex.Match(html, titlePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return StripHtmlTags(match.Groups[1].Value);
            }
            return null;
        }

        private string StripHtmlTags(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return "";
            
            // Remover tags HTML
            var text = Regex.Replace(html, "<.*?>", " ");
            // Limpiar espacios múltiples
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }
    }
}

