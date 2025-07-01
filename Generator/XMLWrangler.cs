using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using DeepL;

namespace UNA_English_Translations;

partial class XMLWrangler
{
    private readonly DeepLClient deepLClient;
    private readonly Program.Settings settings;

    public XMLWrangler(Program.Settings _settings)
    {
        settings = _settings;
        deepLClient = new DeepLClient(settings.DeepLKey);
    }

    public async Task Execute()
    {
        foreach (var (package, def) in settings.ModDefinitions)
        {
            var (output, data) = GenerateLanguageXML();
            var path = Path.Combine(settings.BaseWorkshopPath, def.WorkshopId.ToString(), "Defs");
            foreach (var file in Directory.EnumerateFiles(path, "*.xml", SearchOption.AllDirectories))
            {
                data = def.IsJapanese
                    ? await ConsumeJPDefsFile(file, data)
                    : ConsumeENDefsFile(file, data);
            }

            output.Save(Path.Combine("../Languages/English/DefInjected/ThingDef", $"{package}.xml"));
        }
    }

    private static (XDocument, XElement) GenerateLanguageXML()
    {
        var doc = new XDocument();
        var data = new XElement("LanguageData");
        doc.Add(data);

        return (doc, data);
    }

    private static string GetNodeContents(in string fileIn, in XElement node, in string path)
    {
        Console.WriteLine($"GetNodeContents {node.XPathSelectElement(path)}");
        return node.XPathSelectElement(path)?.Value
            ?? throw new Exception($"{fileIn}: {node.GetAbsoluteXPath()}{path} not found");
    }

    [GeneratedRegex(@"^UNA[_\s]*")]
    private static partial Regex TranslationStripper();

    private static string PrepareForTranslation(in string raw)
    {
        return TranslationStripper().Replace(raw, "");
    }

    private async Task<XElement> ConsumeJPDefsFile(string fileIn, XElement data)
    {
        Debug.Assert(Path.Exists(fileIn));
        var input = XDocument.Load(fileIn);

        var defs = input.XPathSelectElements("/Defs/ThingDef")
            ?? throw new Exception($"{fileIn}: /Defs/ThingDef not found");

        var translations = new SortedDictionary<string, List<XNode>>();
        foreach (var node in defs)
        {
            var defName = GetNodeContents(fileIn, node, "defName");

            foreach (var field in settings.FieldsToTranslate)
            {
                var raw = PrepareForTranslation(GetNodeContents(fileIn, node, field));
                var translated = new XComment("pending...");
                var element = new XElement($"{defName}.{field}");
                translations.Add(raw, [translated, element]);

                data.Add(new XComment($" JP - {raw} "));
                data.Add(translated);
                data.Add(element);
            }
        }

        var results = await deepLClient.TranslateTextAsync(
            translations.Keys,
            LanguageCode.Japanese,
            LanguageCode.EnglishAmerican,
            new TextTranslateOptions { ModelType = ModelType.QualityOptimized }
        );

        var i = 0;
        foreach (var (k, v) in translations)
        {
            var result = results[i].Text;
            i++;

            foreach (var node in v)
            {
                if (node is XElement element)
                    element.Value = result;
                if (node is XComment comment)
                    comment.Value = $" EN (DeepL) - {result} ";
            }
        }

        return data;
    }

    private XElement ConsumeENDefsFile(string fileIn, XElement data)
    {
        Debug.Assert(Path.Exists(fileIn));
        var input = XDocument.Load(fileIn);

        var defs = input.XPathSelectElements("/Defs/ThingDef")
            ?? throw new Exception($"{fileIn}: /Defs/ThingDef not found");

        foreach (var node in defs)
        {
            var defName = GetNodeContents(fileIn, node, "defName");

            foreach (var field in settings.FieldsToTranslate)
            {
                var raw = PrepareForTranslation(GetNodeContents(fileIn, node, field));
                var element = new XElement($"{defName}.{field}");

                element.Value = raw;

                data.Add(new XComment($" EN - {raw} "));
                data.Add(element);
            }
        }

        return data;
    }
}