using Ibit.Core.Data;
using Ibit.Core.Util;
using Ibit.Plataform.Data;
using UnityEngine;
using UnityEngine.UI;
using Ibit.Core.Serial;
using Assets._Game.Scripts.Core.Api.Dto;
using Ibit.Core.Data.Manager;

namespace Ibit.MainMenu.UI
{
    public class StageLoader : MonoBehaviour
    {
        [SerializeField]
        private SerialControllerPitaco serialControllerPitaco;

        [SerializeField]
        private SerialControllerMano serialControllerMano;

        [SerializeField]
        private SerialControllerCinta serialControllerCinta;

        private bool PitacoConnected = false;
        private bool ManoConnected = false;
        private bool CintaConnected = false;

        public StageModel stage;

        private void OnEnable()
        {
            this.GetComponent<Button>().onClick.AddListener(OnStageSelected);

        }

        private async void OnStageSelected()
        {

            //Olhando se o Pitaco está conectado

            if (serialControllerPitaco == null)
                serialControllerPitaco = FindObjectOfType<SerialControllerPitaco>();

            if (serialControllerPitaco.IsConnected)
                PitacoConnected = true;

            //Olhando se o Mano está conectado
            if (serialControllerMano == null)
                serialControllerMano = FindObjectOfType<SerialControllerMano>();

            if (serialControllerMano.IsConnected)
                ManoConnected = true;

            //Olhando se a Cinta está conectada
            if (serialControllerCinta == null)
                serialControllerCinta = FindObjectOfType<SerialControllerCinta>();

            if (serialControllerCinta.IsConnected)
                CintaConnected = true;



            if (PitacoConnected == false && ManoConnected == false && CintaConnected == false)
            {
                SysMessage.Warning("Nenhum dispositivo de controle conectado! Conecte antes de jogar!");
                return;
            }



            if (!Pacient.Loaded.CalibrationPitacoDone && PitacoConnected == true)
            {
                SysMessage.Warning("Calibração não foi feita: PITACO!");
                return;
            }

            if (!Pacient.Loaded.CalibrationManoDone && ManoConnected == true)
            {
                SysMessage.Warning("Calibração não foi feita: MANO!");
                return;
            }

            if (!Pacient.Loaded.CalibrationCintaDone && CintaConnected == true)
            {
                SysMessage.Warning("Calibração não foi feita: CINTA!");
                return;
            }

#if !UNITY_EDITOR

            // Retirar esta parte, acredito que não seja necessaria
            // if (!FindObjectOfType<Ibit.Core.Serial.SerialController>().IsConnected)
            // {
            //     SysMessage.Warning("Pitaco não está conectado! Conecte antes de jogar!");
            //     return;
            // }

#endif

            StageModel.Loaded = stage;
            // DeepDDA: Gameparameters
            MapStageFromDto(FillStageList.stageDto);
            FindObjectOfType<SceneLoader>().LoadScene(1);
            //Debug.Log($"Stage {stage.Id} loaded.");
        }

        // DeepDDA: Mapeamento dos parâmetros
        public void MapStageFromDto(StageDto stageDto)
        {
            StageModel.Loaded.IdApi = stageDto.Id;
            StageModel.Loaded.PacientIdApi = stageDto.PacientId;
            StageModel.Loaded.Id = stageDto.StageId;
            StageModel.Loaded.Phase = stageDto.Phase;
            StageModel.Loaded.Level = stageDto.Level;
            StageModel.Loaded.ObjectSpeedFactor = stageDto.ObjectSpeedFactor;
            StageModel.Loaded.Loops = stageDto.Loops;
            StageModel.Loaded.HeightIncrement = stageDto.HeightIncrement;
            StageModel.Loaded.HeightUpThreshold = stageDto.HeightUpThreshold;
            StageModel.Loaded.HeightDownThreshold = stageDto.HeightDownThreshold;
            StageModel.Loaded.SizeIncrement = stageDto.SizeIncrement;
            StageModel.Loaded.SizeUpThreshold = stageDto.SizeUpThreshold;
            StageModel.Loaded.SizeDownThreshold = stageDto.SizeDownThreshold;
        }
    }
}