using System.Collections;
using System.Linq;
using Ibit.Core.Data;
using Ibit.Core.Database;
using UnityEngine;
using UnityEngine.UI;
using Assets._Game.Scripts.Core.Api.Dto;
using Ibit.Core.Data.Manager;

namespace Ibit.MainMenu.UI
{
    public class FillStageList : MonoBehaviour
    {
        [SerializeField] private StageDb stageDb;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private Scrollbar scrollbar;
        public static StageDto stageDto;
        private bool _populated;

        private void OnEnable()
        {
            if (_populated) return;

            if (stageDb == null || stageDb.StageList == null || stageDb.StageList.Length == 0 || buttonPrefab == null)
            {
                Debug.LogError("FillStageList: StageDb, StageList ou prefab dos botões não está configurado.");
                return;
            }

            if (Pacient.Loaded == null)
            {
                Debug.LogWarning("FillStageList: selecione um paciente antes de carregar as fases.");
                return;
            }

            _populated = true;
            PopulateStageList();
        }

        private async void PopulateStageList()
        {
            // DeepDDA: keep remote parameters when available. Offline responses
            // contain no data; in that case the serialized StageDb settings remain
            // the source of the initial stage configuration.
            stageDto = null;
            try
            {
                var gameparameterResponse = await DataManager.Instance.GetGameparameter(Pacient.Loaded.IdApi);
                stageDto = gameparameterResponse?.Data?.FirstOrDefault();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"FillStageList: não foi possível consultar parâmetros remotos: {exception.Message}");
            }

            if (stageDto == null)
            {
                Debug.LogWarning("FillStageList: parâmetros remotos indisponíveis. " +
                    "Usando os parâmetros originais do StageDb para execução local de desenvolvimento; " +
                    "não equivalem a uma configuração clínica validada.");
            }

            foreach (var stage in stageDb.StageList)
            {
                var item = Instantiate(buttonPrefab);
                item.transform.SetParent(this.transform);
                item.transform.localScale = Vector3.one;
                item.name = $"ITEM_F{stage.Phase}_L{stage.Level}";
                item.AddComponent<StageLoader>().stage = stage;
                if(stageDto != null && stageDto.Phase == stage.Phase){
                    item.GetComponentInChildren<Text>().text = $"Fase: {stageDto.Phase} - Nível: {stageDto.Level}";
                }
                else{
                    item.GetComponentInChildren<Text>().text = $"Fase: {stage.Phase} - Nível: {stage.Level}";  
                }
                item.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter; // Texto alinhado na horizontal e vertical
                item.GetComponent<Button>().interactable = Pacient.Loaded.UnlockedLevels >= stage.Id;
            }

            StartCoroutine(AdjustGrip());
        }

        //DeepDDA: alterado StageDb.Instance.StageList
        // private void OnEnable()
        // {
        //     if (_populated)
        //     {
        //         var children = (from Transform child in transform select child.gameObject).ToList();
        //         children.ForEach(Destroy);
        //     }

        //     //StageDb.Instance.LoadStages();

        //     foreach (var stage in StageDb.StageList) // 
            // {
            //     var item = Instantiate(buttonPrefab);
            //     item.transform.SetParent(this.transform);
            //     item.transform.localScale = Vector3.one;
            //     item.name = $"ITEM_F{stage.Phase}_L{stage.Level}";
            //     item.AddComponent<StageLoader>().stage = stage;
            //     item.GetComponentInChildren<Text>().text = $"Fase: {stage.Phase} - Nível: {stage.Level}";
            //     item.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter; // Texto alinhado na horizontal e vertical
            //     item.GetComponent<Button>().interactable = Pacient.Loaded.UnlockedLevels >= stage.Id;
            // }

        //     StartCoroutine(AdjustGrip());

        //     _populated = true;
        // }

        private IEnumerator AdjustGrip()
        {
            yield return null;
            scrollbar.value = 1;
        }
    }
}