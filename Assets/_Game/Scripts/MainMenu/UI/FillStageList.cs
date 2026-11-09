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
            if (_populated || _loading)
                return;

            if (stageDb == null || stageDb.StageList == null || stageDb.StageList.Length == 0)
            {
                Debug.LogError("[Fases] StageDb sem fases configuradas. Verifique o StageDb da cena.");
                return;
            }

            PopulateStageList();
        }

        private async void PopulateStageList()
        {
            _loading = true;
            stageDto = null; // Nunca reutilizar parametros remotos de outro paciente/sessao.
            try
            {
                if (Pacient.Loaded == null)
                {
                    Debug.LogError("[Fases] Nenhum paciente carregado. Carregue um perfil primeiro.");
                    return;
                }

                // A API pode responder com Data vazio, null, erro ou indisponibilidade.
                // Isso nao deve impedir que o menu apresente os modelos locais de fase.
                try
                {
                    var response = await DataManager.Instance.GetGameparameter(Pacient.Loaded.IdApi);
                    if (response != null && response.Success && response.Data != null)
                        stageDto = response.Data.FirstOrDefault(x => x != null);

                    if (stageDto == null)
                    {
                        string reason = response == null ? "resposta nula" :
                            "success=" + response.Success +
                            ", itens=" + (response.Data != null ? response.Data.Count.ToString() : "null") +
                            ", mensagem=" + (response.Message ?? "(sem mensagem)");
                        Debug.LogWarning("[Fases/DeepDDA] Parametros remotos indisponiveis (" +
                                         reason + "). Usando fases locais do StageDb para teste; " +
                                         "nenhum parametro remoto sera inventado.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Fases/DeepDDA] Falha na consulta de parametros: " +
                                     ex.Message + ". Usando fases locais do StageDb para teste.");
                }

                // A resposta da API pode chegar depois de trocar de tela/cena.
                if (this == null)
                    return;

            
            foreach (var stage in stageDb.StageList)
            {
                if (stage == null)
                {
                    Debug.LogWarning("[Fases] Entrada nula no StageDb ignorada.");
                    continue;
                }

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

                _populated = true;
                StartCoroutine(AdjustGrip());
            }
            catch (Exception ex)
            {
                Debug.LogError("[Fases] Falha ao preencher lista de fases: " + ex);
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