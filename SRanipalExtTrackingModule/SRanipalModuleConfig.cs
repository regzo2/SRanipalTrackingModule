using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using VRCFaceTracking;
using Microsoft.Extensions.Logging;

namespace SRanipalExtTrackingModule
{
    public class ModuleConfig
    {
        public string[] validTrackingModels =
        {
            "Native",
            "V1",
            "V2",
        };
        public string TrackingModelName { get; set; } = "V2";
        public bool UseEyeTracking { get; set; } = true;
        public bool UseLipTracking { get; set; } = true;
    }
    public static class ModuleJSONLoader
    {

        public const string fileName = "SRanipalModule.config";
        private static JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
        };
        private static string FullPath => Path.Combine(VRCFaceTracking.Core.Utils.UserAccessibleDataDirectory, fileName);
        public static void CreateFile()
        {
            using (var writer = File.CreateText(FullPath))
            {
                var contents = JsonSerializer.Serialize(new ModuleConfig(), options);
                writer.Write(contents);
            }
        }

        public static ModuleConfig LoadConfig(ILogger logger) 
        {
            logger.LogInformation("Loading configuration file.");
            try
            {
                var contents = File.ReadAllText(FullPath);
                return JsonSerializer.Deserialize<ModuleConfig>(contents);
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Creating new configuration file in {FullPath}.");
                CreateFile();
            }

            return new ModuleConfig();
        }
    }
}
