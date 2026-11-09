using System.Collections;
using UnityEngine;

namespace Ibit.Core.Serial
{
    // Classe parcial: estes metodos pertencem ao SerialControllerPitaco24.
    // Mapeamento conforme o firmware do PITACO 2.4 (USB).
    public partial class SerialControllerPitaco24
    {
        [ContextMenu("PITACO 2.4 - Identificar")]
        public void SendIdentify() => SendSerialMessage("i");

        [ContextMenu("PITACO 2.4 - Iniciar coleta")]
        public void StartSampling() => SendSerialMessage("r");

        public void StartSamplingDelayed() => StartCoroutine(StartSamplingCoroutine());

        private IEnumerator StartSamplingCoroutine()
        {
            yield return new WaitForSeconds(1.5f);
            StartSampling();
        }

        [ContextMenu("PITACO 2.4 - Parar coleta")]
        public void StopSampling() => SendSerialMessage("f");

        [ContextMenu("PITACO 2.4 - Recalibrar")]
        public void Recalibrate() => SendSerialMessage("c");

        [ContextMenu("PITACO 2.4 - Zeragem de 10 segundos")]
        public void ZeroPressure() => SendSerialMessage("z");
    }
}
