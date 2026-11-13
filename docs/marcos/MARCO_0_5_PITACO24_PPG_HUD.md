# Marco 0.5 — PITACO 2.4: BPM e SpO2 DEMO exibidos na HUD do I Blue It

**Data do ensaio relatado:** 2026-11-11 (horario de Brasilia)  
**Repositorio:** `PHV00/iblueit-psychophysiological-flow-pitaco2.4-port`  
**Branch de desenvolvimento:** `feature/ppg-csharp-bpm-spo2-mvp`  
**Estado funcional anterior ao registro:** `bdce26dfd468688d448d8c5414f3825d06bddf1d`  
**Natureza do marco:** demonstracao de ponta a ponta, observada no Unity Editor com o PITACO 2.4 fisico; nao e validacao clinica.

## Evidencia de funcionamento

Captura da tela do jogo **Plataform** fornecida pelo desenvolvedor nesta conversa:

- HUD no canto superior direito: `HR [2.4]: 59.3 bpm`.
- HUD no canto superior direito: `SpO2 DEMO: 99.8 %`.
- Cena de jogo ativa, com elementos e placar visiveis.
- O teste foi relatado pelo desenvolvedor como bem-sucedido.

Os valores acima sao **um instantaneo ilustrativo de funcionamento**, nao um registro de referencia fisiologica nem prova de exatidao. A captura foi fornecida na conversa e nao integra este commit; este documento preserva seus valores observados.

## O que este marco comprova

1. O firmware do PITACO 2.4 disponibilizou dados opticos suficientes para que o fluxo C# chegasse a estimativas.
2. O pipeline C# do Unity possui calculo de BPM no infravermelho (autocorrelacao) e SpO2 de demonstracao pela razao AC/DC de RED e IR.
3. A ponte `Player.OximetryHudText` apresentou os resultados no componente visual ja existente do jogo.
4. A HUD de oximetria e **legada/reutilizada do I Blue It**, e nao uma nova tela independente. A implementacao adaptou a origem e a formatacao para identificar o PITACO 2.4.

### Caminho do dado

`PitacoConnectionManager` (USB exclusiva)
-> `SerialControllerPitaco24.OnJsonReceived`
-> `PpgProcessingCoordinator` (contato, janela de 8 s, estimativas)
-> `Plataform.Player.UpdatePitaco24Oximetry`
-> `Player.OximetryHudText`
-> `OximeterMeasuresPlataform` / componente `UnityEngine.UI.Text`.

## Arquivos relevantes no estado do marco

- `Assets/_Game/Scripts/Core/Serial/PitacoConnectionManager.cs`: identifica PITACO 2.4 e distribui linhas na unica porta COM.
- `Assets/_Game/Scripts/Core/Serial/SerialControllerPitaco24.cs`: contabiliza JSON e publica evento.
- `Assets/_Game/Scripts/Core/Oximetry/PpgProcessingCoordinator.cs`: contato optico, janela de amostras, diagnosticos e estimativas.
- `Assets/_Game/Scripts/Core/Oximetry/BpmEstimator.cs`: BPM experimental por autocorrelacao de IR.
- `Assets/_Game/Scripts/Core/Oximetry/Spo2Estimator.cs`: SpO2 DEMO nao calibrada.
- `Assets/_Game/Scripts/Plataform/Player/PlayerControl.cs`: formata e publica os numeros no ritmo legado.
- `Assets/_Game/Scripts/Plataform/UI/OximeterMeasuresPlataform.cs`: apresenta o texto na HUD existente.
- `Config/_signalTreatment.csv`: `O;10` (limite de 10 publicacoes numericas/minuto = intervalo de 6 s, apos a primeira leitura valida).

## Teste de regressao sugerido

1. Abrir `Assets/_Game/Scenes/MainMenu.unity` e entrar no modo Play.
2. Conectar PITACO 2.4 na COM, confirmar a mensagem de identificacao.
3. Entrar na Plataforma e manter dedo estabilizado sobre o sensor PPG por pelo menos 8 s; aguardar ate aparecerem HR [2.4] e SpO2 DEMO.
4. Conferir no Console `JSON serial`, `callbacks`, `samples`, `semContato`, `buffer` e estimativas.
5. Remover dedo: valores antigos devem ser invalidados e a HUD deve indicar ausencia de contato.
6. Desconectar o PITACO e confirmar retorno do estado de conexao.
7. Conferir que pressão respiratoria, menus, alvos e ML-Agents permanecem funcionais.

## Limites e pendencias

- **SpO2 DEMO nao e calibrada e nao deve ser interpretada como medida clinica.**
- O registro visual nao substitui comparacao com equipamento de referencia, validacao experimental ou ensaio com multiplos participantes.
- A logica de contato optico do firmware e o filtro de continuidade podem rejeitar janelas inadequadas: isso e esperado.
- A etapa de SQI/G1 validado e sua integracao formal continuam pendentes.
- O dado do PITACO 2.4 permanece restrito a apresentacao/diagnostico: nao alimentar alertas clinicos, recompensas nem DeepDDA.
- Este commit registra o marco e **nao modifica algoritmos, firmware, cena, prefab, COM ou comportamento do jogo**.
