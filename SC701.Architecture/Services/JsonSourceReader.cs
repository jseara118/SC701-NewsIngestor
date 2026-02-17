using System.Net.Http;
using System.Text.Json;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.Architecture.Services
{
    /// <summary>
    /// Implementación para leer fuentes JSON
    /// HU-18: Normalizar contenido al formato acordado
    /// </summary>
    public class JsonSourceReader : ISourceReader
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly INormalizationService _normalizationService;

        public JsonSourceReader(IHttpClientFactory httpClientFactory, INormalizationService normalizationService)
        {
            _httpClientFactory = httpClientFactory;
            _normalizationService = normalizationService;
        }

        public bool CanHandle(string componentType)
        {
            return componentType.Equals("api", StringComparison.OrdinalIgnoreCase) ||
                   componentType.Equals("json", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<StandardNewsItemDto>> ReadFromSourceAsync(Source source, string? secretValue = null)
        {
            var items = new List<StandardNewsItemDto>();

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
                
                // Agregar headers si requiere secret
                if (source.RequiresSecret && !string.IsNullOrWhiteSpace(secretValue))
                {
                    request.Headers.Add("Authorization", $"Bearer {secretValue}");
                }

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync();
                
                // Intentar deserializar como array o objeto único
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Verificar si es un array
                if (jsonContent.TrimStart().StartsWith("["))
                {
                    var jsonArray = JsonSerializer.Deserialize<JsonElement[]>(jsonContent, jsonOptions);
                    if (jsonArray != null)
                    {
                        foreach (var item in jsonArray)
                        {
                            var normalized = _normalizationService.NormalizeFromJson(
                                item.GetRawText(),
                                source.Id.ToString(),
                                source.Name,
                                source.Url,
                                source.ComponentType
                            );
                            items.Add(normalized);
                        }
                    }
                }
                else
                {
                    // Es un objeto único
                    var normalized = _normalizationService.NormalizeFromJson(
                        jsonContent,
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
                // Log error pero continuar con otras fuentes
                Console.WriteLine($"Error leyendo fuente JSON {source.Name}: {ex.Message}");
            }

            return items;
        }
    }
}

