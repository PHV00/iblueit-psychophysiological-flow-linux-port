using System;
using System.Collections;
using Assets._Game.Scripts.Core.Api;
using Ibit.Core.Data;
using Ibit.Core.Data.Manager;
using Ibit.Core.Util;
using Ibit.Core.Database;
using Ibit.Plataform;
using UnityEngine;
using UnityEngine.UI;
using Ibit.Core.Serial;
//using MixerFusion;

namespace Ibit.Core.Game
{
    public partial class MixerManager : MonoBehaviour
    {
        public Parameters CurrentParameters;

        private SerialControllerPitaco scp;
        private SerialControllerMano scm;
        private SerialControllerCinta scc;
        private SerialControllerOximetro sco;

        void Start()
        {
            if (scp == null)
                scp = FindObjectOfType<SerialControllerPitaco>();
    
            if (scm == null)
                scm = FindObjectOfType<SerialControllerMano>();

            if (scc == null)
                scc = FindObjectOfType<SerialControllerCinta>();
            
            if (sco == null)
                sco = FindObjectOfType<SerialControllerOximetro>();
        
            // DeepDDA: evento
            DeepDDAManager.OnDifficultyAdjustmentSpeed += SpeedAdjustment;
            CurrentParameters = ParametersDb.parameters;
        }

        void Update()
        {
            

            if ((scp != null && scp.IsConnected) ||
                (scm != null && scm.IsConnected) ||
                (scc != null && scc.IsConnected)) // Qualquer controle respiratório
            {
                FusionManager(Player.sensorValuePitaco, Player.sensorValueMano, Player.sensorValueCinta, Player.sensorValueOxiSPO, Player.sensorValueOxiHR);
                AdaptationGrid(Player.sensorValuePitaco, Player.sensorValueMano, Player.sensorValueCinta, Player.sensorValueOxiSPO, Player.sensorValueOxiHR);
                
                // Debug.Log($"Pitaco: {Player.sensorValuePitaco}");
                // Debug.Log($"Mano: {Player.sensorValueMano}");
                // Debug.Log($"Cinta: {Player.sensorValueCinta}");
                // Debug.Log($"OxímetroManager: {Player.sensorValueOxiSPO}");

                // FusionManager();
                // Debug.Log("Passei FusionManager");
                // AdaptationGrid();
                // Debug.Log("Passei AdaptationGrid");
                // FissionManager();
                // Debug.Log("Passei FissionManager");
            }
        }

        // DeepDDA: Ajuste de Volocidade
        private void SpeedAdjustment(int adjustSpeed)
        {
            if(_spawner != null && _spawner.SpawnedObjects != null)
            {
                foreach (var obj in _spawner.SpawnedObjects)
                {
                    var moveObject = obj?.GetComponent<MoveObject>();
                    if(moveObject != null && adjustSpeed < 0)
                        moveObject.Speed -= 1.00f;
                    else if(moveObject != null && adjustSpeed > 0)
                        moveObject.Speed += 1.00f;
                }
            }
            else
            {
                Debug.LogError("SpeedAdjustment chamado, mas _spawner é nulo ou SpawnedObjects é nulo.");
            }
        }
        
        protected void OnDestroy()
        {
            DeepDDAManager.OnDifficultyAdjustmentSpeed -= SpeedAdjustment;
        } 
    }
}