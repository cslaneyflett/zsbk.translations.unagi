using Microsoft.Extensions.Configuration;

namespace UNA_English_Translations;

partial class Program
{
    public sealed class Settings
    {
        public sealed class ModDefinition
        {
            public required ulong WorkshopId { get; set; }
            public required bool IsJapanese { get; set; }
        }
        public required string DeepLKey { get; set; }
        public required string BaseWorkshopPath { get; set; }
        public required List<string> FieldsToTranslate { get; set; }
        public required Dictionary<string, ModDefinition> ModDefinitions { get; set; }
    }

    static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var settings = config.GetRequiredSection("Settings").Get<Settings>()
            ?? throw new Exception("Configuration is missing Settings section");

        var wrangler = new XMLWrangler(settings);
        await wrangler.Execute();
    }
}