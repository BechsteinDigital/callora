namespace Callora.Host.Backend.Application.Mail;

/// <summary>
/// Minimal placeholder templating for mail bodies: "{{name}}" tokens are
/// replaced from the model; unknown tokens stay literal so problems surface.
/// </summary>
public static class MailTemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> model)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(model);

        var result = template;
        foreach (var (key, value) in model)
        {
            result = result.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
        }

        return result;
    }
}
