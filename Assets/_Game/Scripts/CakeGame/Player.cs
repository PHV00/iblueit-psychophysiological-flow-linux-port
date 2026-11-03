using Ibit.Core.Serial;
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
        public bool Pitaco24ComboBpmSource { get; private set; }
       


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

        private void OnPitaco24ComboReceived(Pitaco24ComboFrame sample)
        {
            var independent = FindObjectOfType<SerialControllerOximetro>();
            if (independent != null && independent.IsConnected)
                return;

            Pitaco24ComboBpmSource = sample.HasValidBpm;
            oxiActive = sample.HasValidBpm;
            HRValue = sample.HasValidBpm ? sample.Bpm : 0f;
            SPO2Value = 0f;
        }

        private void OnMessageReceivedOximetro(string msg)
        {
             if (msg.Length < 1)
                return;

            Pitaco24ComboBpmSource = false;
            oxiActive = true;
            string[] sensorValueOxi = msg.Split(',');
            HRValue = Parsers.Float(sensorValueOxi[0]);
            SPO2Value = Parsers.Float(sensorValueOxi[1]);
            
        }

        private void Start() 
        {

            var scp = FindObjectOfType<SerialControllerPitaco>();
            var scm = FindObjectOfType<SerialControllerMano>();
            var scc = FindObjectOfType<SerialControllerCinta>();
            var sco = FindObjectOfType<SerialControllerOximetro>();


            if (scp.IsConnected) // Se Pitaco conectado
            {
                scp.OnSerialMessageReceived += OnMessageReceivedPitaco;
                scp.OnPitaco24ComboReceived += OnPitaco24ComboReceived;

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