using Ibit.Core.Serial;
using NaughtyAttributes;
using UnityEngine;

namespace Ibit.Plataform
{
    public partial class Player : MonoBehaviour
    {
        public int HeartPoints => heartPoints;

        //private GameAgent mlGameAgent;

        [SerializeField]
        [BoxGroup("Properties")]
        private int heartPoints = 6;

        private SimulatedInput simulatedInput;

        private void Awake()
        {
            var scp = FindObjectOfType<SerialControllerPitaco>();
            var scm = FindObjectOfType<SerialControllerMano>();
            var scc = FindObjectOfType<SerialControllerCinta>();
            var sco = FindObjectOfType<SerialControllerOximetro>();


            scp.OnSerialMessageReceived += PositionOnSerialPitaco;
            scp.OnSerialMessageReceived += AnimatePitaco;
            scp.OnPitaco24ComboReceived += PositionOnSerialPitaco24;

            scm.OnSerialMessageReceived += PositionOnSerialMano;
            scm.OnSerialMessageReceived += AnimateMano;

            scc.OnSerialMessageReceived += PositionOnSerialCinta;
            scc.OnSerialMessageReceived += AnimateCinta;

            // O antigo oxímetro é opcional no modo PITACO 2.4 Combo.
            if (sco != null)
                sco.OnSerialMessageReceived += PositionOnSerialOximetro;

            //DeepDDA: Ativar bloco de código para Treinamento do Agente
            //simulatedInput = FindObjectOfType<SimulatedInput>();
        }

        private void Start()
        {
            //mlGameAgent = GameAgent.instance;
        }

        private void Update()
        {
#if UNITY_EDITOR
            Move();
#endif
        }
    }
}