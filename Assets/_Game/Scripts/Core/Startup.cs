using System;
using System.IO;
using Assets._Game.Scripts.Core.Configuration;
using Ibit.Core.Data.Constants;
using Ibit.Core.Data.Manager;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets._Game.Scripts.Core
{
    public class Startup
    {
        private const string PreviousApiEndpoint = "https://iblueit.azurewebsites.net/api";
        private const string CurrentApiEndpoint =
            "https://iblueit-server-side-bbfqegaygagse8b5.switzerlandnorth-01.azurewebsites.net/api";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]

        static void OnBeforeSceneLoadRuntimeMethod()
        {
            if (!Directory.Exists(GameDataPaths.configurationPath))
                Directory.CreateDirectory(GameDataPaths.configurationPath);

            string configPath = Path.Combine(GameDataPaths.configurationPath, "config.json");
            if (!File.Exists(configPath))
            {
                using (StreamWriter file = File.CreateText(configPath))
                {
                    var serializer = new JsonSerializer();
                    ConfigurationManager.GameVolume = 100f;
                    ConfigurationManager.SendRemoteData = true;
                    ConfigurationManager.ApiEndpoint = CurrentApiEndpoint;
                    serializer.Serialize(file, ConfigurationManager.Instance);
                }
            }
            else
            {
                DataManagerUtil.LoadJsonFile<ConfigurationManager>(configPath);
                ValidateConfigurationValues();
                MigrateLegacyApiEndpoint(configPath);
            }
        }

        private static void MigrateLegacyApiEndpoint(string configPath)
        {
            // Preserve tokens and all user-specific settings: change only the
            // old endpoint (or a missing one), never a custom deployment URL.
            try
            {
                var settings = JObject.Parse(File.ReadAllText(configPath));
                string savedUrl = (string)settings["apiEndpoint"];
                if (!string.IsNullOrWhiteSpace(savedUrl) &&
                    !string.Equals(savedUrl.Trim().TrimEnd('/'), PreviousApiEndpoint,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ConfigurationManager.ApiEndpoint = savedUrl.Trim().TrimEnd('/');
                    return;
                }

                ConfigurationManager.ApiEndpoint = CurrentApiEndpoint;
                settings["apiEndpoint"] = CurrentApiEndpoint;
                File.WriteAllText(configPath, settings.ToString(Formatting.Indented));
                Debug.Log("[Startup] Endpoint da API atualizado para o App Service atual.");
            }
            catch (Exception ex)
            {
                // A file-write problem must not reinstate the obsolete address
                // for this run; a future launch can retry persistence.
                if (string.IsNullOrWhiteSpace(ConfigurationManager.ApiEndpoint) ||
                    string.Equals(ConfigurationManager.ApiEndpoint.Trim().TrimEnd('/'),
                        PreviousApiEndpoint, StringComparison.OrdinalIgnoreCase))
                    ConfigurationManager.ApiEndpoint = CurrentApiEndpoint;
                Debug.LogWarning("[Startup] Nao foi possivel migrar config.json: " +
                    ex.Message);
            }
        }

        private static void ValidateConfigurationValues()
        {
            if (ConfigurationManager.GameVolume < 0f || ConfigurationManager.GameVolume > 100f)
                ConfigurationManager.GameVolume = 100;
        }
    }
}