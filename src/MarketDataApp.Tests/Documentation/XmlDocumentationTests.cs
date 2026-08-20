using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MarketDataApp.Tests.Documentation;

/// <summary>
/// Enforces the documentation contract on the endpoint surface (#21). CS1591 plus
/// <c>TreatWarningsAsErrors</c> guarantee that XML documentation is <em>present</em>; these tests
/// guard its <em>content</em> by inspecting the generated <c>MarketDataApp.xml</c>: every public
/// endpoint method must carry a real summary, a description for every parameter, a returns tag,
/// at least one exception tag, and a canonical docs-site URL, and every method-name group must
/// include at least one example.
/// </summary>
public sealed class XmlDocumentationTests
{
    private static readonly Type[] ApiClasses =
    [
        typeof(StocksApi),
        typeof(OptionsApi),
        typeof(FundsApi),
        typeof(MarketsApi),
        typeof(UtilitiesApi),
    ];

    private const string PlaceholderSummary = "Executes the endpoint request.";

    private static readonly Regex CanonicalUrl = new(
        @"^https://www\.marketdata\.app/docs/sdk/csharp/[a-z0-9-]+(/[a-z0-9-]+)*/$",
        RegexOptions.CultureInvariant);

    [Fact]
    public void EveryEndpointMethod_CarriesTheFullDocumentationContract()
    {
        var members = LoadMembers();
        var violations = new List<string>();

        foreach (var method in EndpointMethods())
        {
            var id = DocId(method);
            if (!members.TryGetValue(id, out var member))
            {
                violations.Add($"{id}: no XML documentation entry.");
                continue;
            }

            var summary = member.Element("summary")?.Value.Trim();
            if (string.IsNullOrEmpty(summary))
            {
                violations.Add($"{id}: empty <summary>.");
            }
            else if (summary.Contains(PlaceholderSummary, StringComparison.Ordinal))
            {
                violations.Add($"{id}: placeholder <summary>.");
            }

            var documentedParameters = member.Elements("param")
                .ToDictionary(p => (string?)p.Attribute("name") ?? string.Empty, p => p.Value.Trim());
            foreach (var parameter in method.GetParameters())
            {
                if (!documentedParameters.TryGetValue(parameter.Name!, out var text) || text.Length == 0)
                {
                    violations.Add($"{id}: parameter '{parameter.Name}' is undocumented.");
                }
            }

            if (string.IsNullOrEmpty(member.Element("returns")?.Value.Trim()))
            {
                violations.Add($"{id}: missing <returns>.");
            }

            if (!member.Elements("exception").Any(e => !string.IsNullOrEmpty((string?)e.Attribute("cref"))))
            {
                violations.Add($"{id}: no <exception> tags.");
            }

            var seealsoHrefs = member.Elements("seealso")
                .Select(e => (string?)e.Attribute("href"))
                .Where(href => href is not null)
                .ToList();
            if (!seealsoHrefs.Any(href => CanonicalUrl.IsMatch(href!)))
            {
                violations.Add($"{id}: no <seealso href> with a canonical docs URL.");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EveryEndpointGroup_CarriesAtLeastOneExample()
    {
        var members = LoadMembers();
        var missing = EndpointMethods()
            .GroupBy(method => (Type: method.DeclaringType!.Name, Method: method.Name))
            .Where(group => !group.Any(method =>
                members.TryGetValue(DocId(method), out var member)
                && member.Element("example")?.Element("code") is not null))
            .Select(group => $"{group.Key.Type}.{group.Key.Method}")
            .ToList();

        Assert.True(missing.Count == 0, "Endpoint groups without an <example>: " + string.Join(", ", missing));
    }

    private static IReadOnlyList<MethodInfo> EndpointMethods()
    {
        var methods = ApiClasses
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName)
            .ToList();
        Assert.NotEmpty(methods);
        return methods;
    }

    private static Dictionary<string, XElement> LoadMembers()
    {
        var path = Path.ChangeExtension(typeof(MarketDataClient).Assembly.Location, ".xml");
        Assert.True(File.Exists(path), $"XML documentation file not found at {path}.");
        return XDocument.Load(path)
            .Root!
            .Element("members")!
            .Elements("member")
            .ToDictionary(member => (string)member.Attribute("name")!, member => member);
    }

    // Standard XML documentation ID for a non-generic method: M:Full.Type.Name(Param.Type,...)
    // with generic parameter types written as Definition{Arg,...} and no parentheses when the
    // method takes no parameters.
    private static string DocId(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var name = $"M:{method.DeclaringType!.FullName}.{method.Name}";
        return parameters.Length == 0
            ? name
            : $"{name}({string.Join(",", parameters.Select(p => FormatType(p.ParameterType)))})";
    }

    private static string FormatType(Type type)
    {
        if (type.IsArray)
        {
            return FormatType(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition().FullName!;
            definition = definition[..definition.IndexOf('`')];
            var arguments = string.Join(",", type.GetGenericArguments().Select(FormatType));
            return $"{definition.Replace('+', '.')}{{{arguments}}}";
        }

        return type.FullName!.Replace('+', '.');
    }
}
