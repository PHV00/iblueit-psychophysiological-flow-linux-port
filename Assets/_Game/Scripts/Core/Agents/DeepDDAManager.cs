using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ibit.Core.Database;
using Ibit.Core.Data;
using Ibit.Plataform;
using Ibit.Plataform.Manager.Score;
using Ibit.Plataform.Manager.Spawn;
using Ibit.Plataform.Data;
using Ibit.Plataform.Manager.Stage;
using Ibit.Plataform.Logger;
using Ibit.Plataform.UI;
using Ibit.Core.Game;
using Ibit.Core;
using Ibit.Core.Serial;
using Ibit.Core.Data.Enums;
using Ibit.Core.Util;

public class DeepDDAManager: DeepDDAAgent
{
    public event Action<float, float> OnUpdatedPerformanceTarget;
    public event Action<float, float> OnUpdatedPerformanceObstacle;
    public static event Action<int> OnDifficultyAdjustmentSpeed;
    public event Action<float, float> OnUpdatedPerformanceLevel;
    public static DeepDDAManager instance { get; private set; }
    private float expHeightAcc;
    private float expSizeAcc;
    private float insHeightAcc;
    private float insSizeAcc;
    private float heightIncrement;
    private float objectSpeedFactor;

    private void Start()
    {
        heightIncrement = StageModel.Loaded.HeightIncrement;
        objectSpeedFactor = StageModel.Loaded.ObjectSpeedFactor;
    }

    protected void OnDestroy()
    {

        //
    }  

    public enum ObjectType
    {
        Target,
        Obstacle
    }

    // RF-02.01 - AJUSTE DE DIFICULDADE POR MEIO DA VELOCIDADE
    public void SpeedAdjustment(int adjustSpeed)
    {
        if(adjustSpeed < 0)
        {
            if((StageModel.Loaded.ObjectSpeedFactor * ParametersDb.parameters.ObjectsSpeedFactor) > 1)
            {
                StageModel.Loaded.ObjectSpeedFactor -= 0.5f;
                OnDifficultyAdjustmentSpeed?.Invoke(adjustSpeed);
            }
        }
        else if(adjustSpeed > 0 && (StageModel.Loaded.ObjectSpeedFactor * ParametersDb.parameters.ObjectsSpeedFactor) < 3)
            {
                StageModel.Loaded.ObjectSpeedFactor += 0.5f;
                OnDifficultyAdjustmentSpeed?.Invoke(adjustSpeed);
            }
    }

    // RF-02.02 - AJUSTE DE CARGA POR MEIO DA POSIÇÃO E TAMANHO DOS OBJETOS
    public void AdjustObjects(string objectTag, int objectsThreshold)
    {
        switch (objectTag)
            {
                case "AirTarget":
                    insHeightAcc = heightIncrement;
                    insHeightAcc = objectsThreshold < 0 ? -insHeightAcc : insHeightAcc;
                    OnUpdatedPerformanceTarget?.Invoke(insHeightAcc, expHeightAcc);
                    insHeightAcc = 0;
                    break;

                case "WaterTarget":
                    expHeightAcc = heightIncrement;
                    expHeightAcc = objectsThreshold < 0 ? -expHeightAcc : expHeightAcc;
                    OnUpdatedPerformanceTarget?.Invoke(insHeightAcc, expHeightAcc);
                    expHeightAcc = 0;
                    break;

                case "AirObstacle":
                    insSizeAcc = StageModel.Loaded.SizeIncrement;
                    insSizeAcc = objectsThreshold < 0 ? -insSizeAcc : insSizeAcc;
                    OnUpdatedPerformanceObstacle?.Invoke(insSizeAcc, expSizeAcc);
                    insSizeAcc = 0;
                    break;

                case "WaterObstacle":
                    expSizeAcc = StageModel.Loaded.SizeIncrement;
                    expSizeAcc = objectsThreshold < 0 ? -expSizeAcc : expSizeAcc;
                    OnUpdatedPerformanceObstacle?.Invoke(insSizeAcc, expSizeAcc);
                    expSizeAcc = 0;
                    break;
            }
    }

    public void LevelSucceeded()
    {
        OnUpdatedPerformanceLevel?.Invoke(heightIncrement, heightIncrement);
        //Debug.Log("MANAGER ObjectSucceededLevel");
    }

    // RF-02.03 - AJUSTE DE CARGA ENTRE  SESSÕES POR MEIO DO DESEMPENHO
    //RF-02.04 - AJUSTE DE CARGA ENTRE SESSÕES POR MEIO DA ESCALA DE BORG
    public void AdjustSessionDifficulty(int success)
    {
        // se manter ou melhorar o Desempenho do Jogador (∆DJ) por Si sessões consecutivas.
        if (success == 1) // Si
        {
            AdjustForSessionSuccess();
        }
        else if (success == -1) // se falhar em cumprir com o Número e Repetições (NR) da sessão.
        {
            AdjustForSessionFailure(); // VER como usar ParametersDb.parameters.lostWtimes
        }

    }
    private void AdjustForSessionSuccess()
    {
        StageModel.Loaded.HeightIncrement += 0.1f;
        StageModel.Loaded.SizeIncrement += 0.1f;
        //Debug.Log("MANAGER HeightIncrement DeepDDAManager - Desempenho do Jogador (∆DJ)");
    }
    private void AdjustForSessionFailure()
    {
        if(StageModel.Loaded.HeightIncrement > 0.1f)
        {
            StageModel.Loaded.HeightIncrement -= 0.1f; 
        }
        StageModel.Loaded.SizeIncrement -= 0.1f;
        //Debug.Log("MANAGER HeightIncrement DeepDDAManager - Falha do Jogador (∆DJ) em cumprir com (NR)");
    }
  
    // // Método de feedback de biosinais
    // public void ProvideBiosignalFeedback(float rr)
    // {
    //     // Verificar valores de RR
    //     if (rr < 12 || rr > 20)
    //     {
    //         Debug.Log("Frequência respiratória fora do limite seguro.");
    //         TriggerSecurityPanel("Security Panel 3");
    //         SpeedAdjustment(-1);
    //         AddReward(-0.5f);
    //     }
    // }

    // // Método para monitorar a frequência respiratória
    // public float MonitorRespiratoryRate(List<FlowData> flowDatas, int duration)
    // {
    //     var flowDataDictionary = ConvertFlowDataToDictionary(flowDatas);
    //     return PitacoFlowMath.RespiratoryRate(flowDatas, duration);
    // }
    // private Dictionary<float, float> ConvertFlowDataToDictionary(List<FlowData> flowDatas)
    // {
    //     return flowDatas.ToDictionary(fd => fd.Time, fd => fd.Value);
    // }

    // // Método para acionar o painel de segurança
    // public void TriggerSecurityPanel(string panelName)
    // {
    //     GameObject panel = GameObject.Find("Canvas").transform.Find(panelName).gameObject;
    //     if (panel != null && !panel.activeSelf)
    //     {
    //         panel.SetActive(true);
    //         CanvasManager canvasManager = FindObjectOfType<CanvasManager>();
    //         if (canvasManager != null)
    //         {
    //             canvasManager.PauseGametoShowAlert();
    //         }
    //     }
    // }

    
    // public void AdjustBorgScaleLoad(int borgScale)
    // {
    //     // Diminui a carga se EB entre 7 e 10 em Sp sessões
    //     if (borgScale == 0) 
    //     {
    //         //AdjustObjects(?);
    //         Debug.Log("MANAGER Escala de Borg, diminui dificuldade");
    //     }
    //     else if (borgScale == 1) // Aumenta a carga se EB entre 0 e 6 em Si sessões
    //     {
    //         //AdjustObjects(?);
    //         Debug.Log("MANAGER Escala de Borg, aumenta dificuldade");
    //     }
    // }

}
