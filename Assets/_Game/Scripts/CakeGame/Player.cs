using Ibit.Core.Serial;
using Ibit.Core.Oximetry;
using Ibit.Core.Database;
using Ibit.Core.Util;
using UnityEngine;

namespace Ibit.CakeGame
{

    public class Player : MonoBehaviour
    {

        public Candles candle;
        public Stat flow;
        public float picoExpiratorio;
        public Stars score;
        public ScoreMenu scoreMenu;
        public float sensorValuePitaco;
        public float sensorValueMano;
        public float sensorValueCinta;
        public bool stopedFlow;

        public float HRValue;
        public float SPO2Value;
        public bool oxiActive = false;

        // O Player de cada cena continua controlando o HUD legado.
        // A nova fonte 2.4 apenas atualiza campos VISUAIS (nao o Mixer).
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
                    ? Ibit.Plataform.Player.FormatPitaco24Oximetry(
                        pitaco24DisplayedBpm, pitaco24DisplayedSpO2)
                    : $"HR: {HRValue} bpm\nSPO2: {SPO2Value} %";
            }
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
                }
                return;
            }

            pitaco24OxiSourceActive = true;
            oxiActive = true; // Fonte conectada; nao significa SpO2 validada.

            if (pitaco24PpgCoordinator == null)
                pitaco24PpgCoordinator = manager.GetComponent<PpgProcessingCoordinator>();

            var measurement = pitaco24PpgCoordinator != null
                ? pitaco24PpgCoordinator.LastMeasurement : null;

            if (pitaco24PpgCoordinator == null ||
                !pitaco24PpgCoordinator.OpticalContactConfirmed ||
                measurement == null ||
                (!measurement.BpmEstimate.HasValue &&
                 !measurement.SpO2DemoPercent.HasValue))
            {
                pitaco24DisplayedBpm = null;
                pitaco24DisplayedSpO2 = null;
                pitaco24VisualPublished = false;
                nextPitaco24DisplayTime = 0f;
                HRValue = 0f;
                SPO2Value = 0f;
                return;
            }

            // Um canal indisponivel nao pode permanecer com valor anterior.
            if (!measurement.BpmEstimate.HasValue) pitaco24DisplayedBpm = null;
            if (!measurement.SpO2DemoPercent.HasValue)
                pitaco24DisplayedSpO2 = null;

            // 1-second visual refresh; retain immediate invalidation above.
            if (!pitaco24VisualPublished || Time.unscaledTime >= nextPitaco24DisplayTime)
            {
                pitaco24DisplayedBpm = measurement.BpmEstimate;
                pitaco24DisplayedSpO2 = measurement.SpO2DemoPercent;
                pitaco24VisualPublished = true;
                nextPitaco24DisplayTime =
                    Time.unscaledTime + Ibit.Plataform.Player.Pitaco24HudIntervalSeconds;
            }

            HRValue = pitaco24DisplayedBpm.HasValue
                ? (float)pitaco24DisplayedBpm.Value : 0f;
            SPO2Value = pitaco24DisplayedSpO2.HasValue
                ? (float)pitaco24DisplayedSpO2.Value : 0f;
        }

       


        private void OnMessageReceivedPitaco(string msg)
        {
            if (msg.Length < 1)
                return;

            sensorValuePitaco = Parsers.Float(msg);

            if (sensorValuePitaco > 0 && picoExpiratorio < sensorValuePitaco)
                picoExpiratorio = sensorValuePitaco;
            
        }

        private void OnMessageReceivedMano(string msg)
        {
            if (msg.Length < 1)
                return;

            sensorValueMano = Parsers.Float(msg);

            if (sensorValueMano > 0 && picoExpiratorio < sensorValueMano)
                picoExpiratorio = sensorValueMano;
            
        }

        private void OnMessageReceivedCinta(string msg)
        {
            if (msg.Length < 1)
                return;

            sensorValueCinta = Parsers.Float(msg);

            if (sensorValueCinta > 0 && picoExpiratorio < sensorValueCinta)
                picoExpiratorio = sensorValueCinta;
            
        }

        private void OnMessageReceivedOximetro(string msg)
        {
             if (msg.Length < 1)
                return;

            oxiActive = true;
            string[] sensorValueOxi = msg.Split(',');
            HRValue = Parsers.Float(sensorValueOxi[0]);
            SPO2Value = Parsers.Float(sensorValueOxi[1]);
            
        }

        private void Update()
        {
            UpdatePitaco24Oximetry();
        }

        private void Start()
        {
            var scp = FindObjectOfType<SerialControllerPitaco>();
            var scm = FindObjectOfType<SerialControllerMano>();
            var scc = FindObjectOfType<SerialControllerCinta>();
            var sco = FindObjectOfType<SerialControllerOximetro>();


            if (scp.IsConnected) // Se Pitaco conectado
            {
                FindObjectOfType<SerialControllerPitaco>().OnSerialMessageReceived += OnMessageReceivedPitaco;

            } else {
            if (scm.IsConnected) // Se Mano conectado
            {
                FindObjectOfType<SerialControllerMano>().OnSerialMessageReceived += OnMessageReceivedMano;

            } else {
            if (scc.IsConnected) // Se Cinta conectada
            {
                FindObjectOfType<SerialControllerCinta>().OnSerialMessageReceived += OnMessageReceivedCinta;

            }}}

            if (sco.IsConnected) // Se Cinta conectada
            {
                FindObjectOfType<SerialControllerOximetro>().OnSerialMessageReceived += OnMessageReceivedOximetro;

            }
            

        }
    }
}