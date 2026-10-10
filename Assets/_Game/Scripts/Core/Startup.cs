using System.IO;
using Assets._Game.Scripts.Core.Configuration;
using Ibit.Core.Data.Constants;
using Ibit.Core.Data.Manager;
using Newtonsoft.Json;
using UnityEngine;

namespace Assets._Game.Scripts.Core
{
    public class Startup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]

        static void OnBeforeSceneLoadRuntimeMethod()
        {
            // Debug.Log("Before scene loaded");
            if (!Directory.Exists(GameDataPaths.configurationPath))
                Directory.CreateDirectory(GameDataPaths.configurationPath);

            if (!File.Exists($"{GameDataPaths.configurationPath}/config.json"))
            {
                using (StreamWriter file = File.CreateText($"{GameDataPaths.configurationPath}/config.json"))
                {
                    var serializer = new JsonSerializer();
                    ConfigurationManager.GameVolume = 100f;
                    ConfigurationManager.SendRemoteData = true;
                    //Default route to API. I Blue it V5
                    ConfigurationManager.ApiEndpoint = "https://iblueit.azurewebsites.net/api";
                    serializer.Serialize(file, ConfigurationManager.Instance);
                }
            }
            else
            {
                DataManagerUtil.LoadJsonFile<ConfigurationManager>($"{GameDataPaths.configurationPath}/config.json");
                ValidateConfigurationValues();
            }
        }

        private static void ValidateConfigurationValues()
        {
            if (ConfigurationManager.GameVolume < 0f || ConfigurationManager.GameVolume > 100f)
                ConfigurationManager.GameVolume = 100;
        }
    }
}