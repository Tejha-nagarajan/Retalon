using System.Text.Json;
using Retalon.DTOs.Products;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class OpenFoodFactsService : IOpenFoodFactsService
{
    private readonly HttpClient _httpClient;

    public OpenFoodFactsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ProductResponseDto>> SearchProductsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<ProductResponseDto>();
        }

        var url =
            $"https://world.openfoodfacts.org/cgi/search.pl" +
            $"?search_terms={Uri.EscapeDataString(searchTerm)}" +
            $"&search_simple=1" +
            $"&action=process" +
            $"&json=1" +
            $"&page_size=20";

        using var response = await _httpClient.GetAsync(
            url,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new List<ProductResponseDto>();
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty(
                "products",
                out var products))
        {
            return new List<ProductResponseDto>();
        }

        var result = new List<ProductResponseDto>();

        foreach (var product in products.EnumerateArray())
        {
            var name = GetStringProperty(product, "product_name");

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            result.Add(MapProduct(product));
        }

        return result;
    }

    public async Task<ProductResponseDto?> GetProductByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var url =
            $"https://world.openfoodfacts.org/api/v2/product/" +
            $"{Uri.EscapeDataString(barcode)}";

        using var response = await _httpClient.GetAsync(
            url,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty(
                "product",
                out var product))
        {
            return null;
        }

        var status = document.RootElement.TryGetProperty(
            "status",
            out var statusElement)
            ? statusElement.GetInt32()
            : 0;

        if (status != 1)
        {
            return null;
        }

        var name = GetStringProperty(product, "product_name");

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return MapProduct(product);
    }

    private static ProductResponseDto MapProduct(
        JsonElement product)
    {
        return new ProductResponseDto
        {
            ExternalProductId =
                GetStringProperty(product, "code"),

            Name =
                GetStringProperty(product, "product_name")
                ?? "Unknown Product",

            Barcode =
                GetStringProperty(product, "code"),

            Description =
                GetStringProperty(product, "generic_name"),

            ImageUrl =
                GetStringProperty(product, "image_url"),

            Currency = "USD",

            ImportSource = "OpenFoodFacts"
        };
    }

    private static string? GetStringProperty(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}