using Cottle;

namespace LeadDelivery.Infrastructure.Services;

public static class CottleTemplateRenderer
{
    public static string Render(string template, string leadDocumentJson)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return leadDocumentJson;
        }

        var document = Document.CreateDefault(template).DocumentOrThrow;
        return document.Render(Context.CreateBuiltin(new Dictionary<Value, Value>
        {
            ["lead"] = Value.FromString(leadDocumentJson)
        }));
    }
}