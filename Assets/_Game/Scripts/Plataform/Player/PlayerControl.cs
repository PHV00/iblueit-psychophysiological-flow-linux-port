using Ibit.Core.Data;
using Ibit.Core.Database;
using Ibit.Core.Serial;
using Ibit.Core.Util;
using Ibit.Plataform.Camera;
using UnityEngine;
using Ibit.Core.Game;

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
        // UI: combo fornece HR, mas nao fornece SpO2 validada.
        public bool Pitaco24ComboBpmSource { get; private set; }
        public float auxValuesOxi;

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

        private void PositionOnSerialPitaco24(Pitaco24ComboFrame frame)
        {
            // Priorizar oximetro independente quando ele estiver conectado,
            // preservando o comportamento anterior do jogo.
            if (sco != null && sco.IsConnected)
                return;

            Pitaco24ComboBpmSource = frame.HasValidBpm;
            oxiActive = frame.HasValidBpm;
            HRValue = frame.HasValidBpm ? frame.Bpm : 0f;
            SPO2Value = 0f;  // indisponivel, nunca tratar como 0% medido
            sensorValueOxiHR = HRValue;
            sensorValueOxiSPO = 0f; // nao alimentar AdaptationGrid/alertas
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
                    Pitaco24ComboBpmSource = false;
                    oxiActive = true;
                    string[] auxValuesOxi = msg.Split(',');
                
                    sensorValueOxiHR = Parsers.Float(auxValuesOxi[0]);
                    sensorValueOxiSPO = Parsers.Float(auxValuesOxi[1]);
                
                    HRValue = sensorValueOxiHR; //Variável responsável por mostrar na tela a HR
                    SPO2Value = sensorValueOxiSPO; //Variável responsável por mostrar na tela a SPO2
                } else {
                    oxiActive = false;
                }

                currenttimeOxi = Time.time; // currenttimeOxi é atualizado para o tempo atual (Time.time é um contador de segundos que começa quando o jogo é executado)
            }
        }
    }
}