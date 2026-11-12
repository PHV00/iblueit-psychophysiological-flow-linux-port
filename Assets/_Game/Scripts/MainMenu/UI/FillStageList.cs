using System;
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
        private bool _loading;

        private void OnEnable()
        {
            if (_populated || _loading) return;
            if (stageDb == null || stageDb.StageList == null)
            {
                Debug.LogError("StageDb.StageList nao esta pronta ou e null.");
                return;
            }

            _loading = true;
            PopulateStageList();
        }

        private async void PopulateStageList()
        {
            try
            {
                if (Pacient.Loaded == null)
                {
                    Debug.LogWarning("[Stages] Nenhum paciente selecionado.");
                    return;
                }

                // A request may fail or return no parameters when the API is
                // unavailable. Never access Data[0] without checking its size.
                var gameparameterResponse =
                    await DataManager.Instance.GetGameparameter(Pacient.Loaded.IdApi);
                if (this == null || !isActiveAndEnabled) return;

                var availableStage = gameparameterResponse?.Data?
                    .FirstOrDefault(entry => entry != null);
                if (availableStage == null)
                {
                    stageDto = null;
                    Debug.LogWarning("[Stages] Nenhum parametro de fase retornado pela API. " +
                        "Verifique a conexao e abra novamente o menu para tentar outra vez.");
                    return;
                }

                stageDto = availableStage;
                foreach (var stage in stageDb.StageList)
                {
                    var item = Instantiate(buttonPrefab);
                    item.transform.SetParent(this.transform, false);
                    item.transform.localScale = Vector3.one;
                    item.name = $"ITEM_F{stage.Phase}_L{stage.Level}";
                    item.AddComponent<StageLoader>().stage = stage;
                    var label = item.GetComponentInChildren<Text>();
                    if (stageDto.Phase == stage.Phase)
                        label.text = $"Fase: {stageDto.Phase} - Nível: {stageDto.Level}";
                    else
                        label.text = $"Fase: {stage.Phase} - Nível: {stage.Level}";
                    label.alignment = TextAnchor.MiddleCenter;
                    item.GetComponent<Button>().interactable =
                        Pacient.Loaded.UnlockedLevels >= stage.Id;
                }

                _populated = true;
                StartCoroutine(AdjustGrip());
            }
            catch (Exception ex)
            {
                stageDto = null;
                Debug.LogWarning("[Stages] Falha ao carregar parametros de fase: " +
                    ex.Message);
            }
            finally
            {
                _loading = false;
            }
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