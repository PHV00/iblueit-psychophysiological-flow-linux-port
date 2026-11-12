using System.Collections;
using Ibit.Core.Data;
using Ibit.Core.Serial;
using Ibit.Core.Oximetry;
using Ibit.Core.Database;
using Ibit.Core.Util;
using Ibit.Core.Audio;
using UnityEngine;
using Ibit.Core;
using Ibit.Core.Game;

namespace Ibit.WaterGame
{
    public class Player : MonoBehaviour
    {
        /*Events Declaration*/
        public delegate void HaveStarDelegate(int roundScore, int roundNumber, float pikeValue);
        public event HaveStarDelegate HaveStarEvent;

        public delegate void EnablePlayDelegate();
        public event EnablePlayDelegate EnablePlayEvent;

        /*Player Variables*/
        public float maximumPeak;
        public float sensorValuePitaco;
        public float sensorValueMano;
        public float sensorValueCinta;
        public float sensorValueOximetro;
        public bool flowStoped;

        /*Utility Variables*/
        public bool stop;
        public bool waitSignal;
        public int x;
        public int _roundNumber;
        Coroutine lastCoroutine;

        private float gameMultiplier = GameManager.CapacityMultiplierMinigames;

        private SerialControllerPitaco scp;
        private SerialControllerMano scm;
        private SerialControllerCinta scc;
        private SerialControllerOximetro sco;

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
        private float pitaco24VisualInterval;
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

            if (!pitaco24VisualPublished || Time.time >= nextPitaco24DisplayTime)
            {
                pitaco24DisplayedBpm = measurement.BpmEstimate;
                pitaco24DisplayedSpO2 = measurement.SpO2DemoPercent;
                pitaco24VisualPublished = true;
                nextPitaco24DisplayTime = Time.time + pitaco24VisualInterval;
            }

            HRValue = pitaco24DisplayedBpm.HasValue
                ? (float)pitaco24DisplayedBpm.Value : 0f;
            SPO2Value = pitaco24DisplayedSpO2.HasValue
                ? (float)pitaco24DisplayedSpO2.Value : 0f;
        }



        private void Start()
        {
            int oxiReadingsPerMinute = SignalTreatmentDb.LoadSignalParameters("O");
            pitaco24VisualInterval = oxiReadingsPerMinute > 0
                ? 60f / oxiReadingsPerMinute : 6f;

            scp = FindObjectOfType<SerialControllerPitaco>();
            scm = FindObjectOfType<SerialControllerMano>();
            scc = FindObjectOfType<SerialControllerCinta>();
            sco = FindObjectOfType<SerialControllerOximetro>();

            stop = false;
            waitSignal = false;
            x = 0;
            lastCoroutine = null;
            FindObjectOfType<RoundManager>().AuthorizePlayerFlowEvent += ReceivedMessage;


            if (scp.IsConnected) // Se PITACO conectado
            {
                scp.OnSerialMessageReceived += OnMessageReceivedPitaco;
                scp.StartSamplingDelayed();

            }
            else
            {
                if (scm.IsConnected) // Se MANO conectado
                {
                    scm.OnSerialMessageReceived += OnMessageReceivedMano;
                    scm.StartSamplingDelayed();

                }
                else
                {
                    if (scc.IsConnected) // Se CINTA conectada
                    {
                        scc.OnSerialMessageReceived += OnMessageReceivedCinta;
                        scc.StartSamplingDelayed();

                    }
                }
            }

            if (sco.IsConnected) // Se OXÍMETRO conectado
            {
                sco.OnSerialMessageReceived += OnMessageReceivedOximetro;

            }

        }

        //Sending HaveStar Event
        protected virtual void OnHaveStar(int roundScore, int roundNumber, float pikeValue)
        {
            HaveStarEvent?.Invoke(roundScore, roundNumber, pikeValue);
        }

        //Authorize RoundManager
        protected virtual void OnAuthorize() => EnablePlayEvent?.Invoke();

        //Flow values being received by the serial controller
        private void OnMessageReceivedPitaco(string msg)
        {
            if (msg.Length < 1)
                return;
            // Ajuste da sensibilidade do sensorValuePitaco [3-6]
            if(Parsers.Float(msg) < -5 || Parsers.Float(msg) > 5)
            {
                sensorValuePitaco = Parsers.Float(msg);
            }
        }

        private void OnMessageReceivedMano(string msg)
        {
            if (msg.Length < 1)
                return;

            sensorValueMano = Parsers.Float(msg);
        }

        private void OnMessageReceivedCinta(string msg)
        {
            if (msg.Length < 1)
                return;

            sensorValueCinta = Parsers.Float(msg);
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



        public void ReceivedMessage(bool hasPlayed, int roundNumber)
        {
            //Se é pra jogar waitSignal = true. Senão waitSignal = false
            waitSignal = hasPlayed;
            _roundNumber = roundNumber;

            if (hasPlayed)
                lastCoroutine = StartCoroutine(Flow(roundNumber));

            if (!hasPlayed)
            {
                StopCoroutine(lastCoroutine);
                OnHaveStar(0, roundNumber, 0);
            }
        }

        public void ExecuteNextStep()
        {
            OnAuthorize();
        }
        private IEnumerator Flow(int round)
        {

            scp = FindObjectOfType<SerialControllerPitaco>();
            scm = FindObjectOfType<SerialControllerMano>();
            scc = FindObjectOfType<SerialControllerCinta>();

            if (scp.IsConnected) // Se PITACO conectado
            {
                //While player does not blow.
                while (sensorValuePitaco >= -Pacient.Loaded.PitacoThreshold * 2f)
                {
                    //Debug.Log($"Wait: {sensorValue}");
                    yield return null;
                }

                //Player is blowing, take the highest value.
                while (sensorValuePitaco < -Pacient.Loaded.PitacoThreshold)
                {
                    //Debug.Log($"Blow: {sensorValue}");

                    if (sensorValuePitaco < maximumPeak)
                    {
                        maximumPeak = sensorValuePitaco;
                        //Debug.Log("Novo pico máximo: " + maximumPeak);
                    }

                    //calculate the percentage of the pike.
                    yield return null;
                }

            }
            else
            {
                if (scm.IsConnected) // Se Mano conectado
                {
                    //While player does not blow.
                    while (sensorValueMano >= -Pacient.Loaded.ManoThreshold * 2f)
                    {
                        //Debug.Log($"Wait: {sensorValue}");
                        yield return null;
                    }

                    //Player is blowing, take the highest value.
                    while (sensorValueMano < -Pacient.Loaded.ManoThreshold)
                    {
                        //Debug.Log($"Blow: {sensorValue}");

                        if (sensorValueMano < maximumPeak)
                        {
                            maximumPeak = sensorValueMano;
                            //Debug.Log("Novo pico máximo: " + maximumPeak);
                        }

                        //calculate the percentage of the pike.
                        yield return null;
                    }

                }
                else
                {
                    if (scc.IsConnected) // Se CINTA conectada
                    {
                        //While player does not blow.
                        while (sensorValueCinta >= -Pacient.Loaded.CintaThreshold * 2f)
                        {
                            //Debug.Log($"Wait: {sensorValue}");
                            yield return null;
                        }

                        //Player is blowing, take the highest value.
                        while (sensorValueCinta < -Pacient.Loaded.CintaThreshold)
                        {
                            //Debug.Log($"Blow: {sensorValue}");

                            if (sensorValueCinta < maximumPeak)
                            {
                                maximumPeak = sensorValueCinta;
                                //Debug.Log("Novo pico máximo: " + maximumPeak);
                            }

                            //calculate the percentage of the pike.
                            yield return null;
                        }

                    }
                }
            }

            SoundManager.Instance.PlaySound("Success");

            var roundScore = CalculateFlowPike(maximumPeak);
            WriteMinigameLog(round, roundScore, PitacoFlowMath.ToLitresPerMinute(maximumPeak));
            waitSignal = false;
            maximumPeak = 0f;
            OnAuthorize();
        }

        private void WriteMinigameLog(int round, int roundScore, float flowScore)
        {
            FindObjectOfType<MinigameLogger>().WriteMinigameRound(round, roundScore, flowScore);
        }

        private int CalculateFlowPike(float pikeValue)
        {
            var playerPike = 0f;
            var roundScore = 0;

            if (scp.IsConnected) // Se PITACO conectado
            {
                playerPike = (-Pacient.Loaded.CapacitiesPitaco.InsPeakFlow * gameMultiplier);  //originalmente RawInspeakFlow; alterado->02/10/19

            }
            else
            {
                if (scm.IsConnected) // Se Mano conectado
                {
                    playerPike = (-Pacient.Loaded.CapacitiesMano.InsPeakFlow * gameMultiplier);  //originalmente RawInspeakFlow; alterado->02/10/19

                }
                else
                {
                    if (scc.IsConnected) // Se CINTA conectada
                    {
                        playerPike = (-Pacient.Loaded.CapacitiesCinta.InsPeakFlow * gameMultiplier);  //originalmente RawInspeakFlow; alterado->02/10/19

                    }
                }
            }




            var percentage = -pikeValue / playerPike;

            if (percentage > 1.00f)   //Originalmente seria 25,50 e 75%, Foi modificado em 02/10/19 por Diogo e Jhonatan para 33, 67 e 100%
            {
                roundScore = 3;
                OnHaveStar(3, _roundNumber, pikeValue);
            }
            else if (percentage > 0.667f)
            {
                roundScore = 2;
                OnHaveStar(2, _roundNumber, pikeValue);
            }
            else if (percentage > 0.333f)
            {
                roundScore = 1;
                OnHaveStar(1, _roundNumber, pikeValue);
            }


            return roundScore;
        }

        private void Update()
        {
            UpdatePitaco24Oximetry();
            if ((Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Return)) && waitSignal == false)
            {
                ExecuteNextStep();
            }
        }
    }
}
