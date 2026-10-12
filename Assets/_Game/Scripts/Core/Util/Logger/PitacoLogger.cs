using System;
using Ibit.Core.Data;
using Ibit.Core.Game;
using Ibit.Core.Serial;
using System.Linq;
using Assets._Game.Scripts.Core.Api.Extensions;
using Ibit.Core.Data.Enums;
using UnityEngine;

namespace Ibit.Core.Util
{
    public class PitacoLogger : Logger<PitacoLogger>
    {
        public FlowDataDevice flowDataDevice;

        protected override void Awake()
        {
            flowDataDevice = new FlowDataDevice {DeviceName = GameDevice.Pitaco.GetDescription()};

            sb.AppendLine("time;value");
            //DeepDDA: comentar/descomentar bloco de código para Treinamento do Agente
            FindObjectOfType<SerialControllerPitaco>().OnSerialMessageReceived += OnSerialMessageReceived;
            //FindObjectOfType<SimulatedInput>().OnsimulatedSerialMessageReceived += OnSerialMessageReceived; 
        }

        protected override void Save()
        {
            var textData = sb.ToString();

            if (textData.Count(s => s == '\n') < 2)
                return;

            var path = @"savedata/pacients/" + Pacient.Loaded.Id + @"/" + $"{recordStart:yyyyMMdd-HHmmss}_" + FileName + ".csv";
            FileManager.WriteAllText(path, textData);
        }

        private void OnSerialMessageReceived(string msg)
        {
            if (!isLogging || msg.Length < 1 || GameManager.GameIsPaused)
                return;

            // Status/boot/echo messages are not physiological samples.
            // Keep numerical samples unchanged, independently of the OS locale.
            if (!float.TryParse(msg.Trim().Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value) || float.IsNaN(value) || float.IsInfinity(value))
                return;

            flowDataDevice.FlowData.Add(new FlowData
            {
                Date = DateTime.Now,
                Value = value
            });

            sb.AppendLine($"{Time.time:F};{value:F}");
        }
    }
}