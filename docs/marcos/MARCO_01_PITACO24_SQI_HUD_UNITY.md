# Marco 01 — PITACO 2.4: SQI Gate 01 e oximetria experimental no Unity

**Registro:** 12/11/2026 (America/Sao_Paulo)  
**Repositorio:** `PHV00/iblueit-psychophysiological-flow-pitaco2.4-port`  
**Branch de desenvolvimento:** `feature/ppg-csharp-bpm-spo2-mvp`  
**Checkpoint do marco:** `marcos/marco-1`  
**HEAD anterior ao fechamento:** `4f5993ce4fb274efce4a3482fdd283987481080b`  
**Situacao:** concluido **como integracao funcional experimental**. Nao e certificacao de qualidade fisiologica nem validacao clinica.

## Objetivo

Integrar a aquisicao RED/IR do MAX30102 embarcado no PITACO 2.4 ao
processamento Unity C#, com sinal de contato optico, aprovacao
recente do Gate 01 do firmware e apresentacao de BPM e SpO2 DEMO
na HUD ja existente do I Blue It. Manter a logica respiratoria
e o DeepDDA independentes das estimativas experimentais.

## Resultado e evidencia

| Entrega | Situacao e fundamento |
| --- | --- |
| Componente C# BPM/SpO2 DEMO e HUD reutilizada | Implementado; Marco 0.5 registra tela do jogo com `HR [2.4]: 59.3 bpm` e `SpO2 DEMO: 99.8 %`, antes da integracao formal G1 |
| Contato optico `sqi.contact` | Implementado: perda de contato invalida janela/estimativas; monitor de dedo recebe eventos independentes |
| SQI Gate 01 | Implementado em `PpgProcessingCoordinator.cs`: `Waiting`, `Passed`, `Failed`, `Stale` e motivos de reprova |
| Protecao de Gate 01 antigo | Implementado com prazo experimental de 6,5 s, para eventos G1 de ~5 s |
| Janela e recalc. PPG | 8 s de janela movel; estimativas reavaliadas a cada ~400 ms apos janela valida |
| HUD a cada 1 s | Implementada nas cenas Plataforma, WaterGame e CakeGame com `Time.unscaledTime` |
| Compilacao e fluxo no Unity | O desenvolvedor confirmou retomada do funcionamento em 12/11/2026, sem fornecer log de regressao completo nesta etapa |
| Falha de desligamento observada anteriormente | Intermitente; o funcionamento retornou, mas **causa raiz desconhecida**. Nao declarar que foi corrigida pelo ultimo commit |
| Testes automatizados | Testes G1 adicionados aos existentes; **resultado real de execucao do Unity Test Runner nao disponibilizado** |

A revisao dos arquivos e os relatos do desenvolvedor demonstram progresso
de integracao. Nao substituem testes instrumentados, verificacao independente
ou resultados de acuracia.

## Arquitetura consolidada

```text
PITACO 2.4 / MAX30102 (RED + IR)
    |
    | firmware: amostras ~25 Hz; G1 a cada 5 s
    v
USB / uma COM compartilhada, sem oximetro virtual
    |
PitacoConnectionManager
    |
SerialControllerPitaco24.OnJsonReceived
    |
PpgProcessingCoordinator
    |-- sample.sqi.contact: contato optico
    |-- type=g1: PASS/FAIL/WAITING/STALE
    |-- buffer 8 s, recalc. ~400 ms
    |-- BpmEstimator (IR) / Spo2Estimator (DEMO RED/IR)
    |
Player original de cada cena
    |-- publica resultado valido a cada 1 s (Time.unscaledTime)
    v
HUD existente: HR [2.4] / SpO2 DEMO
```

`FingerPlacementMonitor` observa contato e fornece orientacao progressiva
sem depender do Gate 01. O `GamePause` preserva as pausas legadas e o
monitor opcional nao habilita uso clinico das estimativas.

## Contratos e politicas de publicacao

- **Aquisição:** firmware envia `type=sample` com `ppg.red`, `ppg.ir`,
  `seq`, `t_ms` e `sqi.contact`.
- **Gate 01:** firmware envia `type=g1` com booleano `sqi.g1`,
  `reason`, `samples` e `t_ms`; `sample.sqi.g1=null` nao e
  interpretado automaticamente como PASS.
- **G1 pendente:** pode acumular dados, mas nao disponibiliza estimativas.
- **G1 aprovado e recente:** libera publicacao apenas depois dos demais
  criterios matematicos e janela completa de 8 s.
- **G1 reprovado:** limpa a janela e invalida resultados exibidos.
- **G1 expirado:** suspende publicacao ate nova avaliacao;
  `Gate01MaxAgeMs=6500` e parametro provisorio.
- **Contato ausente:** invalida estimativas e exige nova janela/avaliacao.
- **HUD:** `Ibit.Plataform.Player.Pitaco24HudIntervalSeconds=1f`
  somente para PITACO 2.4. A primeira leitura valida aparece sem
  aguardar 1 s adicional; invalidacao nao aguarda o proximo tick.
- **Legado:** `Config/_signalTreatment.csv` continua com `O;10`.
  Nao alteramos amostragem, estimadores, firmware, Mixer ou DeepDDA.

## Rastreabilidade

| Commit | Resultado |
| --- | --- |
| `7085a0dc3` e `docs/marcos/MARCO_0_5_PITACO24_PPG_HUD.md` | Prototipo anterior com HUD funcional |
| `ff92844f3` | Reativacao do bloqueio por contato optico |
| `8231e7585` | Consumo de Gate 01 pelo Unity |
| `0d121e12a` | Testes de regressao G1 |
| `00d9e9e11` | Correcao do erro de compilacao CS0136 |
| `9849705f0` | Endpoint Azure e tratamento de resposta vazia no menu (infraestrutura adjacente) |
| `4f5993ce4` | HUD experimental de 1 s nas tres cenas |

Preservacoes: `marcos/marco-0.5` e
`experiments/ppg-before-g1-unity-gate` documentam etapas anteriores.
A branch `marcos/marco-1` marca este fechamento documental.

## Pendencias, sem atribuir resultados nao medidos

1. Executar `PpgProcessingTests` e `FingerPlacementTests` no
   **Unity Test Runner**, anotando quantidade, falhas e capturas.
2. Capturar 60 s de telemetria e status `Pitaco24`,
   `X,PPG_STATE,COLLECTING`, `sample`, `g1`, `reason`,
   perda de sequencia, contato e BPM/SpO2 DEMO.
3. Confirmar por cronometro a cadencia visual de 1 s em cada cena;
   retirar/recolocar dedo e verificar bloqueio imediato.
4. Simular G1 `Failed` e `Stale`, reconexao USB e ausencia de
   dados; evitar reaproveitar leitura velha.
5. Investigar recorrencia do relato de sensor apagando ao iniciar
   Unity; **nao foi localizada causa definitiva**.
6. Medir falsos positivos/negativos em sinais reais, comparar com
   referencia e realizar posterior validacao fisiologica.

## Criterio para o proximo marco

O Marco 01 fecha a **integracao do prototipo**. A proxima etapa deve
investigar qualidade pulsátil RED/IR com sinais reais e limiares
empiricos, sem converter `SpO2 DEMO` em dado medico ou acionar
alertas, recompensas, seguranca de pacientes ou DeepDDA.

**Alteracoes deste commit:** documentacao do estado atual e links para
versoes historicas; nenhum arquivo de producao, firmware ou hardware
foi modificado.
