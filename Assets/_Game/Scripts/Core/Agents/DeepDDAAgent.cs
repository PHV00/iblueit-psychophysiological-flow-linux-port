using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
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


public class DeepDDAAgent : Agent
{
    public static DeepDDAAgent instance;
    [SerializeField] private StageManager stage;
    [SerializeField] private Scorer scr;
    [SerializeField] private Spawner spwn;
    [SerializeField] private DeepDDAManager deepDDAManager;
    [SerializeField] private Player player;
    [SerializeField] private PitacoLogger pitacoLogger;

    private int consecutiveTargetsHit = -1;
    private int consecutiveTargetsMiss = -1;
    private int consecutiveObstacleMiss = -1;
    private int consecutiveObstacleHit = -1;
    private string objectTag;
    private bool levelSucceeded = false;
    private int sessionSucceeded = 0;
    private int failedTargets = 0;
    private int failedObstacles = 0;  
    private int adjustSpeed = 0;
    private int consecutiveSessionSuccesses = 0;
    private bool stageStart = false;
    private int sizeIncrement = 0;
    private bool hitOccurred = false;
    private GameObject lastHitObject = null;
    public static float currentPerformance = 0;
    public int flowState = 0;
    private float lastPerformance;
    private float performanceDelta;
    private float episodePerformanceDelta;
    private float lastEpisodePerformance = 0;
    private int currentBorgScale;
    private int lastBorgScale;
    private int scaleBorgDelta;
    
    protected new void Awake()
    {
        // //***Inicialização do agente e dos subcomponentes
        // base.Awake();
        // if (instance == null)
        // {
        //     instance = this;
        //     DontDestroyOnLoad(gameObject);
        // }
        // else if (instance != this)
        // {
        //     Destroy(gameObject);
        // }
    }

    void Start () 
    {
        //***Inicialização dos subcomponentes ... confirmar se relmente será necessário
        scr = FindObjectOfType<Scorer>();
        spwn = FindObjectOfType<Spawner>();
        player = FindObjectOfType<Player>();
        pitacoLogger = FindObjectOfType<PitacoLogger>();
        StartCoroutine(SubscribeToEvents());
        lastBorgScale = Pacient.Loaded.CurrentBorgScale;
        lastPerformance = Pacient.Loaded.CurrentPerformance;
    }

    private IEnumerator SubscribeToEvents()
    {   // Coroutine para inscrição em eventos
        while (player == null)
        {
            yield return null; // Espera pelo próximo frame
        }
        while (stage == null)
        {
            yield return null; // Espera pelo próximo frame
        }
        // Inscrer em eventos para receber notificações e tomar decisões
        if(player != null)
        {
            player.OnObjectHit += HandlePlayerHit;
            player.OnPlayerDeath += HandlePlayerDeath;
        }
        else{
            Debug.LogError("Objeto Player é null.");
        }
        if(stage != null)
        {
            stage.OnStageStart += HandleStageStart;
            stage.OnStageEnd += HandleStageEnd;
        }
        else{
            Debug.LogError("Objeto StageManager é null.");
        }
        if(spwn != null)
        {
            spwn.OnTargetHitThreshold += HandleTargetHitThreshold;
            spwn.OnTargetMissThreshold += HandleTargetMissThreshold;
            spwn.OnObstacleMissThreshold += HandleObstacleMissThreshold;
            spwn.OnObstacleHitThreshold += HandleObstacleHitThreshold;            
        }
        else{
            Debug.LogError("Objeto Spawner é null.");
        }
    }

    protected void OnDestroy()
    {
        if(player != null)
        {
            player.OnObjectHit -= HandlePlayerHit;
            player.OnPlayerDeath -= HandlePlayerDeath;
        }
        if(stage != null)
        {
            stage.OnStageStart -= HandleStageStart;
            stage.OnStageEnd -= HandleStageEnd;
        }
        if(spwn != null)
        {
            spwn.OnTargetHitThreshold -= HandleTargetHitThreshold;
            spwn.OnTargetMissThreshold -= HandleTargetMissThreshold;
            spwn.OnObstacleMissThreshold -= HandleObstacleMissThreshold;
            spwn.OnObstacleHitThreshold -= HandleObstacleHitThreshold;   
        }
    }  
    
    // Reset ou configuração do estado inicial para o novo episódio ou DDA para novo episódio
    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();

        // DeepDDA: Ativar bloco de código para Treinamento do Agente
        // var simulatedInput = FindObjectOfType<SimulatedInput>();
        // if (simulatedInput != null)
        // {
        //     simulatedInput.enabled = true;
        // }

        
        lastEpisodePerformance = currentPerformance;

        if (consecutiveTargetsMiss == -StageModel.Loaded.HeightDownThreshold) 
        {
            consecutiveTargetsMiss = 1;
        }
        if (consecutiveObstacleHit == -StageModel.Loaded.SizeDownThreshold) 
        {
            consecutiveObstacleHit = 1;
        }   
        if (consecutiveTargetsHit == StageModel.Loaded.HeightUpThreshold) 
        {
            consecutiveTargetsHit = -1;
        }
        if (consecutiveObstacleMiss == StageModel.Loaded.SizeUpThreshold) 
        {
            consecutiveObstacleMiss = -1;
        } 


        if (levelSucceeded) 
        {
            levelSucceeded = false;
        }

        if(sessionSucceeded != 0)
        {
            sessionSucceeded = 0;
        }

        if(adjustSpeed != 0)
        {
            adjustSpeed = 0;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {   
        sensor.AddObservation(consecutiveTargetsMiss);
        sensor.AddObservation(consecutiveObstacleHit);
        sensor.AddObservation(consecutiveTargetsHit);
        sensor.AddObservation(consecutiveObstacleMiss);
        sensor.AddObservation(StageModel.Loaded.ObjectSpeedFactor);
        if(spwn.TargetsSucceeded != null)
            sensor.AddObservation(spwn.TargetsSucceeded);
        sensor.AddObservation(spwn.TargetsFailed);
        //sensor.AddObservation(spwn.ObstaclesFailed);
        //sensor.AddObservation(spwn.ObstaclesSucceeded);
        sensor.AddObservation(currentPerformance);
        sensor.AddObservation(flowState);
        sensor.AddObservation(scaleBorgDelta);
        sensor.AddObservation(performanceDelta);
        sensor.AddObservation(episodePerformanceDelta);

        // Observa CG - pico e duração.
        if(sessionSucceeded == 0)
        {
            sensor.AddObservation(-Pacient.Loaded.CapacitiesPitaco.InsPeakFlow * GameManager.CapacityMultiplierPlataform);
            sensor.AddObservation(Pacient.Loaded.CapacitiesPitaco.ExpPeakFlow * GameManager.CapacityMultiplierPlataform);    
        }
        if(sessionSucceeded == 0)
        {
            sensor.AddObservation(-Pacient.Loaded.CapacitiesPitaco.InsFlowDuration * GameManager.CapacityMultiplierPlataform);
            sensor.AddObservation(Pacient.Loaded.CapacitiesPitaco.ExpFlowDuration * GameManager.CapacityMultiplierPlataform);            
        }
    }

    private void HandleStageStart()
    {
        stageStart = true;
        if(StageModel.Loaded.HeightIncrement > 0)
            deepDDAManager.LevelSucceeded();
    }
    private void HandleStageEnd()
    {
        if(scr.Result == GameResult.Success)
            sessionSucceeded = 1;
        else
            sessionSucceeded = -1;
        // DeepDDA: Ativar bloco de código para Treinamento do Agente
        //FindObjectOfType<SceneLoader>().LoadScene(1);
    }

    private void HandlePlayerHit(GameObject hitObject)
    {
        // Reage ao colidir com o objeto 'RelaxObject', indicando o fim do Level
        if (hitObject.CompareTag("RelaxObject") && !hitOccurred)
        {
            hitOccurred = true;
            if(currentPerformance >= GameManager.LevelUnlockScoreThreshold)
            {
                levelSucceeded = true;
                StageModel.Loaded.Level++; // REVER para codar
                CanvasManager canvasManager = GameObject.FindObjectOfType<CanvasManager>();
                canvasManager.StageLevel = StageModel.Loaded.Level.ToString();
                
                if((spwn.TargetsSucceeded + spwn.TargetsFailed) % 10 == 0)
                {
                    adjustSpeed = 1;
                }
                
            }
        }
        if (!hitObject.CompareTag("RelaxObject"))
        {
            hitOccurred = false;
        }
        lastHitObject = hitObject;
    }

    private void HandlePlayerDeath()
    {
        // Reagir à morte do Blue
        Debug.Log($"AGENT Morte do Blue HANDLE: ");
    }

    private void HandleTargetHitThreshold(string objectTagHit, int targetsHit)
    {
        consecutiveTargetsHit = targetsHit;
        objectTag = objectTagHit;
    }
    private void HandleTargetMissThreshold(string objectTagMiss, int targetsMiss)
    {
        consecutiveTargetsMiss = targetsMiss;
        objectTag = objectTagMiss;
        adjustSpeed = -1;
    }
    private void HandleObstacleMissThreshold(string objectTagMiss, int obstaclesMiss)
    {
        consecutiveObstacleMiss = obstaclesMiss;
        objectTag = objectTagMiss;
    }
    private void HandleObstacleHitThreshold(string objectTagHit, int obstaclesHit)
    {
        consecutiveObstacleHit = obstaclesHit;
        objectTag = objectTagHit;
        adjustSpeed = -1;
    }

  	public float flowPsico
	{
		get 
		{
            float objSucceeded = spwn.TargetsSucceeded + spwn.ObstaclesSucceeded;
            float objCurrent = spwn.TargetsSucceeded + spwn.TargetsFailed + spwn.ObstaclesSucceeded + spwn.ObstaclesFailed;
            float scoreRatio = 0;
            if(objCurrent != 0)
            {
                scoreRatio = objSucceeded/objCurrent;        
            }
            if(scoreRatio < 0.3)
			{
				return 0; 
			}
			else if(scoreRatio < GameManager.LevelUnlockScoreThreshold)
			{   
                return scoreRatio;
			}
            else
			{
				return 1;
			}
		}
	}
   
    public float flowFisio
	{
		get
		{
            float expPeak = Pacient.Loaded.CapacitiesPitaco.ExpPeakFlow;
            float insPeak = Pacient.Loaded.CapacitiesPitaco.InsPeakFlow;
            var flowDatas = pitacoLogger.flowDataDevice.FlowData;
            float normalizedValueFLi = 0;
            float averageValueFLi = 0;
            float minValueFLi = 0;
            float maxValueFLi = 0;
            float normalizedValueFLe = 0;
            float averageValueFLe = 0;
            float minValueFLe = 0;
            float maxValueFLe = 0;
            if (flowDatas != null && flowDatas.Any())
            {
                var negativeFlows = flowDatas.Where(fd => fd.Value < insPeak * GameManager.CapacityMultiplierPlataform).ToList();
                var positiveFlows = flowDatas.Where(fd => fd.Value > expPeak * GameManager.CapacityMultiplierPlataform).ToList();
                if (negativeFlows.Any())
                {
                    minValueFLi = negativeFlows.Min(fd => fd.Value);
                    maxValueFLi = negativeFlows.Max(fd => fd.Value);
                    averageValueFLi = negativeFlows.Average(fd => fd.Value);
                    normalizedValueFLi = (averageValueFLi - maxValueFLi) / (minValueFLi - maxValueFLi);
                }
                if (positiveFlows.Any())
                {
                    minValueFLe = positiveFlows.Min(fd => fd.Value);
                    maxValueFLe = positiveFlows.Max(fd => fd.Value);
                    averageValueFLe = positiveFlows.Average(fd => fd.Value);
                    normalizedValueFLe = (averageValueFLe - minValueFLe) / (maxValueFLe - minValueFLe);
                }
            }
            if (averageValueFLi < insPeak * GameManager.CapacityMultiplierPlataformMax && averageValueFLe > expPeak * GameManager.CapacityMultiplierPlataformMax)
            {
                return 1;
            }
            if (averageValueFLi > insPeak * GameManager.CapacityMultiplierPlataform || averageValueFLe < expPeak * GameManager.CapacityMultiplierPlataform)
            {
                return 0;
            }
            float flowFisioValue = 0.5f * normalizedValueFLi + 0.5f * normalizedValueFLe;
            return flowFisioValue;
            
            // para obstáculos
            // float expFlowDuration = Pacient.Loaded.CapacitiesPitaco.ExpFlowDuration;
            // float insFlowDuration = Pacient.Loaded.CapacitiesPitaco.InsFlowDuration;
            // float sensorValuePitaco = Player.sensorValuePitaco;
        }
	}

    // Nível de dificuldade atual do jogo (easy, flow ou hard)
    private int GetCurrentFlowState()
	{
        currentPerformance = 0.5f * flowPsico + 0.5f * flowFisio;
        episodePerformanceDelta = currentPerformance - lastEpisodePerformance; 
		if(currentPerformance < GameManager.CapacityMultiplierPlataform || consecutiveTargetsMiss == -3 || consecutiveObstacleHit == -3 || episodePerformanceDelta < -0.1)
			return 2; // hard
        else if(currentPerformance >= GameManager.CapacityMultiplierPlataform && (episodePerformanceDelta >= -0.1 && episodePerformanceDelta <= 0.1))
			return 1; // Flow 
		else if(currentPerformance >= GameManager.LevelUnlockScoreThreshold)
			return 0; // easy
        return -1;
	}

    // Decisão e ação baseada nas observações coletadas
    // public override void OnActionReceived(ActionBuffers actionBuffers)
    // {
    //     flowState = GetCurrentFlowState();
    //     if(flowState == -1) 
    //     {
    //         return;
    //     }
    //     int speedAction = actionBuffers.DiscreteActions[0];
    //     int objectsAction = actionBuffers.DiscreteActions[1];
    //     int sessionAction = actionBuffers.DiscreteActions[2];
    //     //Debug.Log($"Flow State: {flowState} | ActionBuffers - Speed: {speedAction}, Objects: {objectsAction}, Session: {sessionAction}");

    //     // RF-02.01 - AJUSTE DE DIFICULDADE POR MEIO DA VELOCIDADE (treino de resistência)
    //     switch (speedAction)
    //     {            
    //         // Ação para manter nível de dificuldade
    //         case 0:                 
    //             if(flowState == 1 && adjustSpeed != 0)
    //             {
    //                 AddReward(-0.5f / (StageModel.Loaded.Loops * 10));
    //                 Debug.Log("Velocidade: Manter a velocidade do treino.");
    //             }
    //             break;
            
    //         // Ação para diminuir nível de dificuldade se ocorrer Fi falhas consecutivas
    //         case 1:
    //             if((StageModel.Loaded.ObjectSpeedFactor * ParametersDb.parameters.ObjectsSpeedFactor) > 1
    //                 && (episodePerformanceDelta < -0.05 || (adjustSpeed == -1 && consecutiveTargetsMiss == -StageModel.Loaded.HeightDownThreshold)))
    //             {  
    //                 deepDDAManager.SpeedAdjustment(adjustSpeed);
    //                 if (flowState == 2)
    //                 {
    //                     AddReward(1f); // Recompensa por tornar o jogo mais fácil evitando a frustração
    //                     Debug.Log("Velocidade: Diminuir a velocidade do treino.");
    //                 }
    //                 else
    //                 {
    //                     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
    //                     Debug.Log("Penalidade Velocidade: Diminuir a velocidade do treino.");
    //                 }
    //                 EndEpisode();
    //             }
    //             break;
            
    //         // Ação para aumentar nível de dificuldade se ocorrer sucesso no episódio
    //         case 2:
    //             if((StageModel.Loaded.ObjectSpeedFactor * ParametersDb.parameters.ObjectsSpeedFactor) < 3
    //                 && adjustSpeed == 1 && currentPerformance >= GameManager.LevelUnlockScoreThreshold)
    //             {
    //                 deepDDAManager.SpeedAdjustment(adjustSpeed);                    
    //                 if (flowState == 0)
    //                 {
    //                     AddReward(1f); // Recompensa por tornar o jogo mais desafiador evitando o tédio
    //                     Debug.Log("Velocidade: Aumentar a velocidade do treino.");
    //                 }
    //                 else
    //                 {
    //                     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
    //                     Debug.Log("Penalidade Velocidade: Aumentar a velocidade do treino.");
    //                 }
    //                 EndEpisode();
    //             }
    //             break;
    //     }
        
    //     // RF-02.02 - AJUSTE DE DIFICULDADE POR MEIO DA POSIÇÃO E TAMANHO DOS OBJETOS (treino de força)
    //     switch(objectsAction)
    //     {            
    //         // Ação para manter nível de dificuldade
    //         case 0:
    //             if(flowState == 1)
    //             {
    //                 AddReward(-0.5f / (StageModel.Loaded.Loops * 10));
    //                 Debug.Log("Carga: Manter a carga do treino.");
    //             }
    //             break;

    //         // Ação para diminuir nível de dificuldade se ocorrer Fi falhas consecutivas
    //         case 1:
    //             if (consecutiveTargetsMiss == -StageModel.Loaded.HeightDownThreshold || episodePerformanceDelta < -0.05)
    //             { 
    //                 deepDDAManager.AdjustObjects(objectTag, consecutiveTargetsMiss);
    //                 if (flowState == 2)
    //                 {
    //                     AddReward(1f); // Recompensa por tornar o jogo mais fácil evitando a frustração
    //                     Debug.Log("Carga: Diminuir a carga do treino.");
    //                 }
    //                 else
    //                 {
    //                     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
    //                     Debug.Log("Penalidade Carga: Diminuir a carga do treino.");
    //                 }
    //                 EndEpisode();
    //             }
    //             if (consecutiveObstacleHit == -StageModel.Loaded.SizeDownThreshold)
    //             {
    //                 deepDDAManager.AdjustObjects(objectTag, consecutiveObstacleHit);
    //                 if (flowState == 2)
    //                 {
    //                     AddReward(1f); // Recompensa por tornar o jogo mais fácil evitando a frustração
    //                 }
    //                 else
    //                 {
    //                     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
    //                 }
    //                 EndEpisode();
    //             }
    //             break;

    //         // Ação para aumentar nível de dificuldade se ocorrer sucesso no episódio //&& currentPerformance >= GameManager.LevelUnlockScoreThreshold
    //         case 2:
    //             if(consecutiveTargetsHit == StageModel.Loaded.HeightUpThreshold)
    //             {
    //                 deepDDAManager.AdjustObjects(objectTag, consecutiveTargetsHit);
    //                 if (flowState == 0)
    //                 {
    //                     AddReward(1f); // Recompensa por tornar o jogo mais desafiador
    //                     Debug.Log("Carga: Aumentar a carga do treino.");
    //                 }
    //                 else
    //                 {
    //                     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de avaliação
    //                     Debug.Log("Penalidade Carga: Aumentar a carga do treino.");
    //                 }
    //                 EndEpisode();
    //             } 
    //             if(consecutiveObstacleMiss == StageModel.Loaded.SizeUpThreshold && currentPerformance >= GameManager.LevelUnlockScoreThreshold)
    //             {
    //                 deepDDAManager.AdjustObjects(objectTag, consecutiveObstacleMiss);
    //                 if (flowState == 0)
    //                 {
    //                     AddReward(1f); // Recompensa por tornar o jogo mais desafiador
    //                 }
    //                 else
    //                 {
    //                     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de avaliação
    //                 }
    //                 EndEpisode();
    //             } 
    //             break;
    //     }

    //     // VER como usar ResultScreenUI.numberFailures
    //     // VER como usar PlaySessionsDone
    //     // RF-02.03 - AJUSTE DE DIFICULDADE ENTRE SESSÕES POR MEIO DO DESEMPENHO
    //     // RF-02.04 - AJUSTE DE CARGA ENTRE SESSÕES POR MEIO DA ESCALA DE BORG
    //     performanceDelta = currentPerformance - lastPerformance; // verificar lugar e momento
    //     currentBorgScale = PlataformLogger.selectedBorgScale; // verificar lugar e momento
    //     scaleBorgDelta = currentBorgScale - lastBorgScale; // verificar lugar e momento

    //     switch (sessionAction)
    //     {
    //         // Ação para manter nível de dificuldade
    //         case 0:
    //             if(flowState == 1 && sessionSucceeded != 0)
    //             {
    //                 AddReward(-0.5f / (StageModel.Loaded.Loops * 10)); // Punição moderada por manter o desempenho (verificar variável para multiplicar aqui, talvez Razão do Score)
    //                 Debug.Log("Próxima sessão: Manter a carga do treino.");
    //                 EndEpisode();
    //             }                
    //             break;             
            
    //         // Ação para diminuir nível de dificuldade se falhar em cumprir com NR da sessão // && currentPerformance < GameManager.LevelUnlockScoreThreshold
    //         case 1:
    //             if(sessionSucceeded == -1 && performanceDelta <= -0.1 && scaleBorgDelta >= 2)
    //             {
    //                 deepDDAManager.AdjustSessionDifficulty(sessionSucceeded);
    //                 if (flowState == 2)
    //                 {
    //                     AddReward(1f); // Recompensa por tornar o jogo mais fácil evitando a frustração
    //                     Debug.Log("Próxima sessão: Diminuir a carga do treino.");
    //                 }
    //                 else
    //                 {
    //                     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
    //                     Debug.Log("Penalidade Próxima sessão: Diminuir a carga do treino.");
    //                 }
    //                 EndEpisode();
    //             }
    //             break;
            
    //         // Ação para aumentar nível de dificuldade se ocorrer sucesso ∆DJ por Si sessões consecutivas (objetivo g3) // && currentPerformance >= GameManager.LevelUnlockScoreThreshold
    //         case 2:
    //             if (sessionSucceeded == 1 && performanceDelta >= 0.1 && scaleBorgDelta <= -2)
    //             {
    //                 deepDDAManager.AdjustSessionDifficulty(sessionSucceeded);
    //                 if (flowState == 0)
    //                 {
    //                     AddReward(1f); // Recompensa por tornar o jogo mais desafiador
    //                     Debug.Log("Próxima sessão: Aumentar a carga do treino.");
    //                 }
    //                 else
    //                 {
    //                     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
    //                     Debug.Log("Penalidade Próxima sessão: Aumentar a carga do treino.");
    //                 }
    //                 EndEpisode();
    //             }
    //             break;
    //     }
    //     // Monitoramento dos biosinais e feedback
    //     //SPO2Value e HRValue - O Agent e AddReward foi implementado na classe Mixer
    // }   
   
    public override void OnActionReceived(ActionBuffers actionBuffers)
{
    flowState = GetCurrentFlowState();
    if(flowState == -1) 
    {
        return;
    }
    int speedAction = actionBuffers.DiscreteActions[0];
    int objectsAction = actionBuffers.DiscreteActions[1];
    int sessionAction = actionBuffers.DiscreteActions[2];
    //Debug.Log($"Flow State: {flowState} | ActionBuffers - Speed: {speedAction}, Objects: {objectsAction}, Session: {sessionAction}");

    // RF-02.01 - AJUSTE DE DIFICULDADE POR MEIO DA VELOCIDADE (treino de resistência)
    switch (speedAction)
    {            
        // Ação para manter nível de dificuldade
        case 0:                 
            if(adjustSpeed != 0)
            {
                AddReward(-0.5f / (StageModel.Loaded.Loops * 10));
                Debug.Log("Velocidade: Manter a velocidade do treino.");
            }
            break;
        
        // Ação para diminuir nível de dificuldade se ocorrer Fi falhas consecutivas // && consecutiveTargetsMiss == -StageModel.Loaded.HeightDownThreshold && (StageModel.Loaded.ObjectSpeedFactor * ParametersDb.parameters.ObjectsSpeedFactor) > 1
        case 1:
            if(adjustSpeed == -1 || episodePerformanceDelta < -0.05)
            {  
                deepDDAManager.SpeedAdjustment(adjustSpeed);
                // if (flowState == 2)
                // {
                    AddReward(1.2f); // Recompensa por tornar o jogo mais fácil evitando a frustração
                    Debug.Log("Velocidade: Diminuir a velocidade do treino.");
                // }
                // else
                // {
                //     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
                //     Debug.Log("Penalidade Velocidade: Diminuir a velocidade do treino.");
                // }
                EndEpisode();
            }
            break;
        
        // Ação para aumentar nível de dificuldade se ocorrer sucesso no episódio && currentPerformance >= GameManager.LevelUnlockScoreThreshold && (StageModel.Loaded.ObjectSpeedFactor * ParametersDb.parameters.ObjectsSpeedFactor) < 3
        case 2:
            if(adjustSpeed == 1)
            {
                deepDDAManager.SpeedAdjustment(adjustSpeed);                    
                // if (flowState == 0)
                // {
                    AddReward(1.2f); // Recompensa por tornar o jogo mais desafiador evitando o tédio
                    Debug.Log("Velocidade: Aumentar a velocidade do treino.");
                // }
                // else
                // {
                //     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
                //     Debug.Log("Penalidade Velocidade: Aumentar a velocidade do treino.");
                // }
                EndEpisode();
            }
            break;
    }
    
    // RF-02.02 - AJUSTE DE DIFICULDADE POR MEIO DA POSIÇÃO E TAMANHO DOS OBJETOS (treino de força)
    switch(objectsAction)
    {            
        // Ação para manter nível de dificuldade
        case 0:
            if(consecutiveTargetsMiss == -StageModel.Loaded.HeightDownThreshold
                || consecutiveObstacleHit == -StageModel.Loaded.SizeDownThreshold
                || consecutiveTargetsHit == StageModel.Loaded.HeightUpThreshold
                || consecutiveObstacleMiss == StageModel.Loaded.SizeUpThreshold
                || episodePerformanceDelta < -0.1)
            {
                AddReward(-0.5f / (StageModel.Loaded.Loops * 10));
                Debug.Log("Carga: Manter a carga do treino.");
            }
            break;

        // Ação para diminuir nível de dificuldade se ocorrer Fi falhas consecutivas
        case 1:
            if (consecutiveTargetsMiss == -StageModel.Loaded.HeightDownThreshold || episodePerformanceDelta < -0.05)
            { 
                deepDDAManager.AdjustObjects(objectTag, consecutiveTargetsMiss);
                // if (flowState == 2)
                // {
                    AddReward(1.2f); // Recompensa por tornar o jogo mais fácil evitando a frustração
                    Debug.Log("Carga: Diminuir a carga do treino.");
                // }
                // else
                // {
                //     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
                //     Debug.Log("Penalidade Carga: Diminuir a carga do treino.");
                // }
                EndEpisode();
            }
            if (consecutiveObstacleHit == -StageModel.Loaded.SizeDownThreshold)
            {
                deepDDAManager.AdjustObjects(objectTag, consecutiveObstacleHit);
                if (flowState == 2)
                {
                    AddReward(1f); // Recompensa por tornar o jogo mais fácil evitando a frustração
                }
                else
                {
                    AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
                }
                EndEpisode();
            }
            break;

        // Ação para aumentar nível de dificuldade se ocorrer sucesso no episódio //&& currentPerformance >= GameManager.LevelUnlockScoreThreshold
        case 2:
            if(consecutiveTargetsHit == StageModel.Loaded.HeightUpThreshold)
            {
                deepDDAManager.AdjustObjects(objectTag, consecutiveTargetsHit);
                // if (flowState == 0)
                // {
                    AddReward(1.2f); // Recompensa por tornar o jogo mais desafiador
                    Debug.Log("Carga: Aumentar a carga do treino.");
                // }
                // else
                // {
                //     AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de avaliação
                //     Debug.Log("Penalidade Carga: Aumentar a carga do treino.");
                // }
                EndEpisode();
            } 
            if(consecutiveObstacleMiss == StageModel.Loaded.SizeUpThreshold && currentPerformance >= GameManager.LevelUnlockScoreThreshold)
            {
                deepDDAManager.AdjustObjects(objectTag, consecutiveObstacleMiss);
                if (flowState == 0)
                {
                    AddReward(1f); // Recompensa por tornar o jogo mais desafiador
                }
                else
                {
                    AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de avaliação
                }
                EndEpisode();
            } 
            break;
    }

    // VER como usar ResultScreenUI.numberFailures
    // VER como usar PlaySessionsDone
    // RF-02.03 - AJUSTE DE DIFICULDADE ENTRE SESSÕES POR MEIO DO DESEMPENHO
    // RF-02.04 - AJUSTE DE CARGA ENTRE SESSÕES POR MEIO DA ESCALA DE BORG
    performanceDelta = currentPerformance - lastPerformance; // verificar lugar e momento
    currentBorgScale = PlataformLogger.selectedBorgScale; // verificar lugar e momento
    scaleBorgDelta = currentBorgScale - lastBorgScale; // verificar lugar e momento

    switch (sessionAction)
    {
        // Ação para manter nível de dificuldade
        case 0:
            if(sessionSucceeded != 0)
            {
                AddReward(-0.5f / (StageModel.Loaded.Loops * 10)); // Punição moderada por manter o desempenho (verificar variável para multiplicar aqui, talvez Razão do Score)
                Debug.Log("Próxima sessão: Manter a carga do treino.");
                EndEpisode();
            }                
            break;             
        
        // Ação para diminuir nível de dificuldade se falhar em cumprir com NR da sessão // && currentPerformance < GameManager.LevelUnlockScoreThreshold
        case 1:
            if(sessionSucceeded == -1)
                //performanceDelta <= -0.1 && scaleBorgDelta >= 2)
            {
                deepDDAManager.AdjustSessionDifficulty(sessionSucceeded);
                if (flowState == 2)
                {
                    AddReward(1f); // Recompensa por tornar o jogo mais fácil evitando a frustração
                    Debug.Log("Próxima sessão: Diminuir a carga do treino.");
                }
                else
                {
                    AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
                    Debug.Log("Penalidade Próxima sessão: Diminuir a carga do treino.");
                }
                EndEpisode();
            }
            break;
        
        // Ação para aumentar nível de dificuldade se ocorrer sucesso ∆DJ por Si sessões consecutivas (objetivo g3) // && currentPerformance >= GameManager.LevelUnlockScoreThreshold
        case 2:
            if (sessionSucceeded == 1)
                // && performanceDelta >= 0.1 && scaleBorgDelta <= -2)
            {
                deepDDAManager.AdjustSessionDifficulty(sessionSucceeded);
                if (flowState == 0)
                {
                    AddReward(1f); // Recompensa por tornar o jogo mais desafiador
                    Debug.Log("Próxima sessão: Aumentar a carga do treino.");
                }
                else
                {
                    AddReward(-2f); // Penalidade se não for a decisão correta baseada na função de desempenho
                    Debug.Log("Penalidade Próxima sessão: Aumentar a carga do treino.");
                }
                EndEpisode();
            }
            break;
    }
    // Monitoramento dos biosinais e feedback
    //SPO2Value e HRValue - O Agent e AddReward foi implementado na classe Mixer
}   

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        
        // Pressione '1' para manter a velocidade atual. 
        // Pressione '2' para diminuir a velocidade.
        // Pressione '3' para aumentar a velocidade.
        discreteActionsOut[0] = Input.GetKey(KeyCode.Alpha1) ? 0 : 
                                Input.GetKey(KeyCode.Alpha2) ? 1 : 
                                Input.GetKey(KeyCode.Alpha3) ? 2 : 0;

        // Pressione 'Q' para manter o nível de dificuldade (se ∆DJ estiver dentro do intervalo [-|α|,|α|])
        // Pressione 'W' para diminuir a dificuldade devido a falhas.
        // Pressione 'E' para aumentar a dificuldade devido a sucesso (se ∆DJ>α) 
        discreteActionsOut[1] = Input.GetKey(KeyCode.Q) ? 0 : 
                                Input.GetKey(KeyCode.W) ? 1 : 
                                Input.GetKey(KeyCode.E) ? 2 : 0;

        // Pressione 'A' para manter o nível de dificuldade entre sessões.
        // Pressione 'S' para diminuir a dificuldade se falhar em cumprir com o NR da sessão.
        // Pressione 'D' para aumentar a dificuldade se manter ou melhorar o desempenho por sessões consecutivas.
        discreteActionsOut[2] = Input.GetKey(KeyCode.A) ? 0 : 
                                Input.GetKey(KeyCode.S) ? 1 : 
                                Input.GetKey(KeyCode.D) ? 2 : 0;    
    }

}
