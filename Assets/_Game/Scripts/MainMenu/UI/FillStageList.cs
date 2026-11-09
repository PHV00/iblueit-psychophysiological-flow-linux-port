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
            if (!_populated)
            {
                // Certifica-se de que StageDb está instanciado e StageList está pronta
                if (stageDb.StageList != null)
                {
                    PopulateStageList();
                    _populated = true;
                }
                else
                {
                    Debug.LogError("StageDb.StageList não está pronta ou é null.");
                }
            }
        }

        private async void PopulateStageList()
        {
            // DeepDDA: Gameparameters
            var gameparameterResponse = await DataManager.Instance.GetGameparameter(Pacient.Loaded.IdApi);
            stageDto = gameparameterResponse.Data[0];
            
            foreach (var stage in stageDb.StageList)
            {
                var item = Instantiate(buttonPrefab);
                item.transform.SetParent(this.transform);
                item.transform.localScale = Vector3.one;
                item.name = $"ITEM_F{stage.Phase}_L{stage.Level}";
                item.AddComponent<StageLoader>().stage = stage;
                if(stageDto.Phase == stage.Phase){
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