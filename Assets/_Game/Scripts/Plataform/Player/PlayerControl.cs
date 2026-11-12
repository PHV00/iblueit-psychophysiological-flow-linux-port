using Ibit.Core.Data;
using Ibit.Core.Database;
using Ibit.Core.Serial;
using Ibit.Core.Util;
using Ibit.Plataform.Camera;
using UnityEngine;
using Ibit.Core.Game;
using Ibit.Core.Oximetry;
using System.Globalization;

namespace Ibit.Plataform
{
    public partial class Player
    {
        private float SignalSamplesRatePitaco;
        private float timeBetweenSamplesPitaco;
        private float currenttimePitaco;

        private float SignalSamplesRateMano;
        private float timeBetweenSamplesMano;
        private float currenttimeMano;

        private float SignalSamplesRateCinta;
        private float timeBetweenSamplesCinta;
        private float currenttimeCinta;

        private float SignalSamplesRateOxi;
        private float timeBetweenSamplesOxi;
        private float currenttimeOxi;

        private float gameMultiplier = GameManager.CapacityMultiplierPlataform;
        
        [SerializeField] private SerialControllerPitaco scp;
        [SerializeField] private SerialControllerMano scm;
        [SerializeField] private SerialControllerCinta scc;
        [SerializeField] private SerialControllerOximetro sco;

        //IMPORTANT Valores principais colhidos dos dispositivos:
        public static float sensorValuePitaco;
        public static float sensorValueMano;
        public static float sensorValueCinta;
        public static float sensorValueOxiSPO;
        public static float sensorValueOxiHR;

        public float HRValue; //Variável responsável por mostrar na tela a HR
        public float SPO2Value; //Variável responsável por mostrar na tela a SPO2
        public float RRValue; //Variável responsável por mostrar na tela a frequência respiratória
        public bool oxiActive = false;
        public float auxValuesOxi;

        // Experimental PITACO 2.4 HUD refresh only. Keep the 8-second PPG
        // estimation window, 5-second G1 and the legacy oxi O;10 schedule intact.
        public const float Pitaco24HudIntervalSeconds = 1f;

        // Only the visual side of Player is bridged in phase 1.
        // The existing STATIC sensorValueOxi* fields remain owned by the
        // legacy oximeter, so the Mixer/DeepDDA are not affected yet.
        private bool pitaco24OxiSourceActive;
        private bool pitaco24VisualPublished;
        private double? pitaco24DisplayedBpm;
        private double? pitaco24DisplayedSpO2;
        private float nextPitaco24DisplayTime;
        private PpgProcessingCoordinator pitaco24PpgCoordinator;

        public string OximetryHudText
        {
            get
            {
                return pitaco24OxiSourceActive
                    ? FormatPitaco24Oximetry(pitaco24DisplayedBpm,
                        pitaco24DisplayedSpO2)
                    : $"HR: {HRValue} bpm\nSPO2: {SPO2Value} %";
            }
        }

        // Shared formatting lives in Player, not in an extra hub/formatter.
        // A missing value is never converted to a misleading numeric zero.
        public static string FormatPitaco24Oximetry(double? bpm, double? spo2Demo)
        {
            string hr = bpm.HasValue
                ? bpm.Value.ToString("F1", CultureInfo.InvariantCulture) : "--";
            string spo2 = spo2Demo.HasValue
                ? spo2Demo.Value.ToString("F1", CultureInfo.InvariantCulture) : "--";
            return $"HR [2.4]: {hr} bpm\nSpO2 DEMO: {spo2} %";
        }

        private void UpdatePitaco24Oximetry()
        {
            var manager = PitacoConnectionManager.Active;
            bool connected = manager != null &&
                manager.ConnectedVersion == PitacoConnectionManager.Version.Pitaco24;

            if (!connected)
            {
                if (pitaco24OxiSourceActive)
                {
                    pitaco24OxiSourceActive = false;
                    pitaco24VisualPublished = false;
                    pitaco24DisplayedBpm = null;
                    pitaco24DisplayedSpO2 = null;
                    nextPitaco24DisplayTime = 0f;
                    oxiActive = false;
                    // A separate legacy oximeter can repopulate the HUD with
                    // its next reading; do not impersonate its serial connection.
                }
                return;
            }

            // A 2.4 connection identifies the source, not a valid measurement.
            pitaco24OxiSourceActive = true;
            oxiActive = true; // Existing HUD scripts remain the entry point.

            if (pitaco24PpgCoordinator == null)
                pitaco24PpgCoordinator = manager.GetComponent<PpgProcessingCoordinator>();

            var sample = pitaco24PpgCoordinator != null
                ? pitaco24PpgCoordinator.LastMeasurement : null;

            if (pitaco24PpgCoordinator == null ||
                !pitaco24PpgCoordinator.OpticalContactConfirmed ||
                sample == null ||
                (!sample.BpmEstimate.HasValue && !sample.SpO2DemoPercent.HasValue))
            {
                // Missing contact, a reset window, timeouts or disconnection
                // immediately hide the old readings; never display stale data.
                pitaco24DisplayedBpm = null;
                pitaco24DisplayedSpO2 = null;
                pitaco24VisualPublished = false;
                nextPitaco24DisplayTime = 0f;
                HRValue = 0f;
                SPO2Value = 0f;
                return;
            }

            // A channel which has become unavailable is cleared immediately,
            // even while the 1-second visual publication interval is running.
            bool invalidated =
                (pitaco24DisplayedBpm.HasValue && !sample.BpmEstimate.HasValue) ||
                (pitaco24DisplayedSpO2.HasValue && !sample.SpO2DemoPercent.HasValue);
            if (invalidated)
            {
                if (!sample.BpmEstimate.HasValue) pitaco24DisplayedBpm = null;
                if (!sample.SpO2DemoPercent.HasValue) pitaco24DisplayedSpO2 = null;
                HRValue = pitaco24DisplayedBpm.HasValue
                    ? (float)pitaco24DisplayedBpm.Value : 0f;
                SPO2Value = pitaco24DisplayedSpO2.HasValue
                    ? (float)pitaco24DisplayedSpO2.Value : 0f;
            }

            // Experimental PITACO 2.4 refreshes at most once per second.
            // The first valid reading appears immediately after the 8-second
            // processing window. Invalid readings are cleared above immediately.
            if (!pitaco24VisualPublished || Time.unscaledTime >= nextPitaco24DisplayTime)
            {
                pitaco24DisplayedBpm = sample.BpmEstimate;
                pitaco24DisplayedSpO2 = sample.SpO2DemoPercent;
                HRValue = pitaco24DisplayedBpm.HasValue
                    ? (float)pitaco24DisplayedBpm.Value : 0f;
                SPO2Value = pitaco24DisplayedSpO2.HasValue
                    ? (float)pitaco24DisplayedSpO2.Value : 0f;
                pitaco24VisualPublished = true;
                nextPitaco24DisplayTime =
                    Time.unscaledTime + Pitaco24HudIntervalSeconds;
            }
        }

        public object outputDoPlayer { get; set; }

        private void OnEnable()
        {
            // Taxa de amostragem do "Tratamento de Sinais" por minuto do Pitaco
            SignalSamplesRatePitaco = SignalTreatmentDb.LoadSignalParameters("P");
            timeBetweenSamplesPitaco = 60 / SignalSamplesRatePitaco;  // 60 segundos / amostragem desejada = tempo de intervalo entre cada amostra do sinal

            // Taxa de amostragem do "Tratamento de Sinais" por minuto do Mano
            SignalSamplesRateMano = SignalTreatmentDb.LoadSignalParameters("M");
            timeBetweenSamplesMano = 60 / SignalSamplesRateMano;  // 60 segundos / amostragem desejada = tempo de intervalo entre cada amostra do sinal

            // Taxa de amostragem do "Tratamento de Sinais" por minuto da Cinta de Pressão
            SignalSamplesRateCinta = SignalTreatmentDb.LoadSignalParameters("C");
            timeBetweenSamplesCinta = 60 / SignalSamplesRateCinta;  // 60 segundos / amostragem desejada = tempo de intervalo entre cada amostra do sinal

            // Taxa de amostragem do "Tratamento de Sinais" por minuto do Oxímetro
            SignalSamplesRateOxi = SignalTreatmentDb.LoadSignalParameters("O");
            timeBetweenSamplesOxi = 60 / SignalSamplesRateOxi;  // 60 segundos / amostragem desejada = tempo de intervalo entre cada amostra do sinal
        }

        private void PositionOnSerialPitaco(string msg)
        {
            if (msg.Length < 1)
                return;

            //DeepDDA: comentar este bloco para Treinamento do Agente
            if (Time.time > currenttimePitaco + timeBetweenSamplesPitaco) // Executa a quantidade de leituras escolhida pelo usuário no arquivo "_signalTreatment.csv" por minuto.
            { 
                sensorValuePitaco = 0f;
                sensorValuePitaco = Parsers.Float(msg);
                sensorValuePitaco = sensorValuePitaco < -Pacient.Loaded.PitacoThreshold || sensorValuePitaco > Pacient.Loaded.PitacoThreshold ? sensorValuePitaco : 0f;

                currenttimePitaco = Time.time; // currenttimePitaco é atualizado para o tempo atual (Time.time é um contador de segundos que começa quando o jogo é executado)
            }

            //DeepDDA: Ativar bloco de código para Treinamento do Agente
            // if (simulatedInput != null)
            // {
            //     sensorValuePitaco = 0f;
            //     sensorValuePitaco = simulatedInput.GetSimulatedSensorValue();
            // }
        }


        private void PositionOnSerialMano(string msg)
        {
            if (msg.Length < 1)
                return;

            if (Time.time > currenttimeMano + timeBetweenSamplesMano) // Executa a quantidade de leituras escolhida pelo usuário no arquivo "_signalTreatment.csv" por minuto.
            { 
                sensorValueMano = 0f;
                sensorValueMano = Parsers.Float(msg);
                sensorValueMano = sensorValueMano < -Pacient.Loaded.ManoThreshold || sensorValueMano > Pacient.Loaded.ManoThreshold ? sensorValueMano : 0f;

                currenttimeMano = Time.time; // currenttimeMano é atualizado para o tempo atual (Time.time é um contador de segundos que começa quando o jogo é executado)
            }
        }


        private void PositionOnSerialCinta(string msg)
        {
            if (msg.Length < 1)
                return;

            if (Time.time > currenttimeCinta + timeBetweenSamplesCinta) // Executa a quantidade de leituras escolhida pelo usuário no arquivo "_signalTreatment.csv" por minuto.
            { 
                sensorValueCinta = 0f;
                sensorValueCinta = Parsers.Float(msg)+(Pacient.Loaded.CapacitiesCinta.ExpPeakFlow * gameMultiplier);
                sensorValueCinta = sensorValueCinta < -Pacient.Loaded.CintaThreshold || sensorValueCinta > Pacient.Loaded.CintaThreshold ? sensorValueCinta : 0f;

                currenttimeCinta = Time.time; // currenttimeCinta é atualizado para o tempo atual (Time.time é um contador de segundos que começa quando o jogo é executado)
            }
        }

        private void PositionOnSerialOximetro(string msg)
        {
            if (msg.Length < 1)
                return;

            if (Time.time > currenttimeOxi + timeBetweenSamplesOxi) // Executa a quantidade de leituras escolhida pelo usuário no arquivo "_signalTreatment.csv" por minuto.
            {
                // Se o Oxímetro estiver conectado
                if (sco.IsConnected)
                {
                    if (!pitaco24OxiSourceActive) oxiActive = true;
                    string[] auxValuesOxi = msg.Split(',');
                
                    sensorValueOxiHR = Parsers.Float(auxValuesOxi[0]);
                    sensorValueOxiSPO = Parsers.Float(auxValuesOxi[1]);
                
                    // Legacy data remain available to the current Mixer;
                    // when 2.4 is connected it owns only the visual fields.
                    if (!pitaco24OxiSourceActive)
                    {
                        HRValue = sensorValueOxiHR;
                        SPO2Value = sensorValueOxiSPO;
                    }
                } else if (!pitaco24OxiSourceActive) {
                    oxiActive = false;
                }

                currenttimeOxi = Time.time; // currenttimeOxi é atualizado para o tempo atual (Time.time é um contador de segundos que começa quando o jogo é executado)
            }
        }
    }
}