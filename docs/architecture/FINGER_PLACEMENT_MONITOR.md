# FingerPlacementMonitor — arquitetura e teste (Unity 2022.3.4f1)

**Branch:** `feature/ppg-csharp-bpm-spo2-mvp`  
**Checkpoint pre-implementacao:** `experiments/ppg-pre-finger-placement`  
**Marco 0.5 preservado:** `marcos/marco-0.5`

## Objetivo e limites

Orientar o posicionamento do dedo no MAX30102 de forma progressiva, sem
esperar a janela PPG de 8 s. Este e um recurso de **usabilidade**, nao um
dispositivo medico ou avaliador de risco fisiologico. A SpO2 continua
**DEMO nao calibrada**, e suas estimativas nao dirigem este monitor,
os alertas clinicos, as recompensas ou o DeepDDA.

O firmware G1, a serial USB e os calculos BPM/SpO2 nao sao alterados.
A branch continua **experimental sem gate de contato para estimadores**.
A orientacao de dedo depende apenas de `sqi.contact` recebido a cada JSON,
independentemente de haver resultados numericos.

## Componentes

| Arquivo | Papel |
| --- | --- |
| `Core/Oximetry/Placement/FingerPlacementState.cs` | Enum e maquina de estados pura, testavel |
| `Core/Oximetry/Placement/FingerPlacementMonitor.cs` | Consome eventos de contato, controla estados e sessao |
| `Shared/UI/FingerPlacementFeedback.cs` | Cria uma sobreposicao Unity UI temporaria nas cenas suportadas |
| `Shared/UI/FingerPlacementPauseBridge.cs` | Adquire e libera exclusivamente a pausa do proprio monitor |
| `Core/Game/GamePause.cs` | Controla separadamente a solicitacao legada e a do monitor |
| `Editor/Unit Tests/FingerPlacementTests.cs` | Regressao da maquina de estados e da propriedade da pausa |

A sobreposicao e criada **em tempo de execucao**, ao entrar em
`Plataform`, `WaterGame` ou `CakeGame`, e destruida ao sair.
Portanto nao e necessario editar os prefabs/arquivos .unity manualmente
nem duplicar paineis nos tres minijogos. Um prefab de tema visual pode
ser criado depois no Editor, se a equipe preferir.

O `PitacoConnectionManager` ja pertence a unica COM e faz bootstrap
persistente do `SerialControllerPitaco24` e do `PpgProcessingCoordinator`.
Ele passa a adicionar o monitor, o adaptador de pausa e a UI no mesmo
GameObject persistente. A UI e um overlay local por cena, sem
`DontDestroyOnLoad`.

## Estados e limiares iniciais

- Sem amostras ou PITACO 2.4 nao identificado: aguardar; nenhuma pausa.
- `sqi.contact == false` de modo continuo por menos de 1 s: tolerancia.
- De 1 s ate menos de 3 s: aviso **amarelo** `Reposicione o dedo`.
- De 3 s ate menos de 6 s: aviso **vermelho**, maior.
- A partir de 6 s, **apenas se o monitoramento for obrigatorio**:
  solicitar pausa orientativa e mostrar botao de retomada.
- Contato volta: `Recovering` por 2 s de sinal continuo;
  se havia pausa, habilitar `Retomar jogo` **somente apos recuperacao**.
- Sem JSON de contato por mais de 2 s: `TelemetryLost` (nao inferir dedo).
- Desconexao: `DeviceDisconnected` (nao inferir dedo).

Os estados usam `Time.unscaledTime`: a avaliacao continua enquanto
`Time.timeScale = 0`. Se a coleta estiver ausente, a recuperacao nao
sera validada por valores antigos.

## Controle de pausa e concorrencia

`GameManager` separa duas solicitacoes: `legacyPauseRequested`
(que inclui menus e paines de seguranca existentes) e
`fingerPlacementPauseRequested`. Pausa real = OU logico de ambos.
Um pedido clinico/manual que chegar enquanto o monitor estiver pausado
tambem registra seu proprio pedido. O monitor **nunca libera uma pausa
legada** e nao toma posse de uma pausa preexistente.

Importante: a infraestrutura legada ainda agrupa *suas proprias*
pausas manuais/clinicas num unico pedido booleano; esta mudanca
nao pretende modernizar todos os tipos de pausa.

## Habilitando a pausa em testes

A pausa do posicionamento e **opcional por padrao**. Durante o
Play Mode no Editor, pressione **F9** para alternar:
`[FingerPlacement] Monitoramento obrigatorio: True/False`.
Em build, chamar `SetMonitoringRequired(bool)` no componente
persistente `FingerPlacementMonitor`, a partir do fluxo de
configuracao da sessao. O Editor nao deve interpretar F9 como
um comando serial.

## Plano de teste (PITACO fisico, Unity Test Runner)

1. Atualizar branch e compilar no Unity 2022.3.4f1.
2. Test Runner > EditMode > `FingerPlacementTests`.
3. Na Plataforma com PITACO 2.4 conectado e JSON em transmissao,
   tirar o dedo por >1 s: banner amarelo.
4. Manter ausente por >3 s: banner vermelho maior.
5. Na modalidade opcional (padrao), aguardar >6 s:
   o jogo continua funcionando (nao pausar).
6. Em Play Mode, pressionar F9 para modo obrigatorio e manter
   dedo ausente >6 s: solicitar pausa e mostrar painel central.
7. Manter o dedo estavel por 2 s com telemetria continua:
   habilitar botao `Retomar jogo`; confirmar que clicar retoma.
8. Pausar o jogo manualmente enquanto o monitor ja possui uma
   pausa, simular uma solicitacao de seguranca e verificar que
   a liberacao da pausa de dedo nao libera a solicitacao legada.
9. Desconectar o PITACO: apresentar mensagem especifica de
   telemetria/conexao, sem declarar erro fisiologico.
10. Navegar entre menu, Plataforma, WaterGame e CakeGame:
    nenhuma sobreposicao em menus, nenhuma UI duplicada.
11. Confirmar que a pressao respiratoria e o controle do
    jogo continuam funcionando na modalidade opcional.
12. Confirmar que o MIXER, recompensas e DeepDDA nao
    receberam BPM ou SpO2 DEMO do experimento.

As verificacoes acima ainda exigem execucao no Unity e com o hardware
real; a inspeccao de codigo e os testes adicionados ao projeto nao
substituem a validacao de runtime.
