# PITACO 2.4 — etapa 1: oximetria pelo Player original
> **Atualizacao 2026-11-12:** o
> [Marco 01 consolidado](marcos/MARCO_01_PITACO24_SQI_HUD_UNITY.md)
> supera o estado descrito originalmente abaixo. O firmware G1 passou a
> condicionar publicacao de estimativas no Unity e a HUD experimental
> agora publica numericos a cada **1 segundo**. A janela de calculo
> continua em 8 s e a cadencia de consumo `O;10` do oximetro legado
> nao foi alterada. Passagens antigas de 6 s ou G1 nao integrado sao historicas.


**Estado:** codigo atualizado na branch experimental; ainda sem compilacao no Unity
ou validacao com hardware. BPM e SpO2 DEMO sao experimentais e nao clinicos.

## Decisao arquitetural

Removidos os componentes redundantes `OximetryIntegrationHub` e
`OximetryHudFormatter`. A infraestrutura existente ja atende o fluxo:

PITACO 2.4 (MAX30105) -> PitacoConnectionManager (unica COM) ->
SerialControllerPitaco24 (JSON) -> PpgProcessingCoordinator (8s, BPM/SpO2 DEMO) ->
Player (campos de exibicao existentes) -> HUD legado.

O `PpgProcessingCoordinator` **permanece**, porque faz a aquisicao/estimacao
e ja publica `LastMeasurement` e `OnMeasurement` para as etapas futuras.

O bootstrap persistente de `PitacoConnectionManager` cria automaticamente
`SerialControllerPitaco24` e `PpgProcessingCoordinator`, sem alterar
cenas e sem outra conexao USB. Cuidado com componentes de controlador
modernos adicionais criados manualmente em alguma cena.

## Contrato de exibicao via Player

`Player.Update()` consulta o coordenador do manager persistente.
`oxiActive` indica que a fonte 2.4 esta conectada, nao que BPM e SpO2
foram validados. `OximetryHudText`, propriedade do proprio Player,
retorna as duas linhas a serem exibidas.

- Sem janela valida, sem contato, sem amostras recentes ou com quebra de
  sequencia: `HR [2.4]: -- bpm / SpO2 DEMO: -- %`.
- Com BPM ou SpO2 disponiveis: o HUD publica cada numero disponivel;
  nunca transforma valor ausente em zero na apresentacao.
- A primeira medicao pode aparecer assim que a janela de 8 s termina.
- Atualizacoes numericas posteriores seguem `O;10` = 6 s, carregado
  de `Config/_signalTreatment.csv`.
- A perda de contato, o reinicio da janela ou a indisponibilidade de
  qualquer canal invalidam a exibicao imediatamente.
- Ao desconectar o 2.4, o HUD retorna ao comportamento do oximetro
  antigo quando houver uma nova leitura dele.

Os tres scripts `OximeterMeasuresPlataform`,
`OximeterMeasuresWaterGame` e `OximeterMeasuresCakeGame` permanecem
na mesma posicao e com a mesma condicao `plr.oxiActive`, apenas lendo
`plr.OximetryHudText` em lugar da interpolacao local de campos.
Nao e preciso criar Text, Canvas, prefab ou componente adicional para HUD.

## Delimitacao da etapa 1

Nao atualizar `Player.sensorValueOxiHR` ou `Player.sensorValueOxiSPO`
a partir do PITACO 2.4 nesta etapa: essas variaveis estaticas alimentam
`MixerManager` e `MixerAdaptationGrid`, incluindo regras de aviso,
dificuldade e recompensa. A origem 2.4 atualiza apenas os campos visuais
de instancia `HRValue`, `SPO2Value` e `oxiActive`; o texto do Player
identifica explicitamente `SpO2 DEMO`.

Na etapa 2, o Mixer deve selecionar a fonte explicitamente e verificar
estado e qualidade antes de qualquer alteracao ativa do jogo. Na etapa 3,
loggers e DTOs do banco podem consumir as estimativas tipadas publicadas
pelo `PpgProcessingCoordinator`, sem hub adicional e sem usar
`BPM,SpO2` como um numero decimal unico.

## Correcao de compilacao dos minijogos

WaterGame e CakeGame possuem classes Player independentes do Player da plataforma. Ambas agora implementam `OximetryHudText` e a leitura do `PpgProcessingCoordinator`, preservando a cadencia `O;10`. A formatacao estatica e reutilizada do Player da plataforma; nenhum novo hub/formatador foi adicionado. Teste adicional confirma o contrato nas tres classes.

## Checklist Unity

1. Atualizar a branch, abrir no Unity 2022.3.4f1 e verificar compilacao.
2. Executar `PpgProcessingTests` no EditMode Test Runner.
3. Conectar PITACO 2.4 com firmware JSON `type: sample`, `sqi.contact`,
   RED/IR. Confirmar handshake e mensagens no Console.
4. Abrir `Plataform`; durante os primeiros 8 s, mostrar `--`;
   depois ver BPM/SpO2 DEMO na interface antiga.
5. Confirmar numeros atualizados aproximadamente a cada 6 s.
6. Remover e recolocar o dedo e testar desconexao/reconexao.
7. Conferir `WaterGame` e `CakeGame` e garantir que dados de 2.4
   nao dispararam Mixer/DeepDDA, alertas ou gravacao de API.
8. Se houver oximetro antigo disponivel, testar o fallback do HUD.

Os testes unitarios nao substituem teste de cena nem validacao clinica.

## Correcao do status 2.4 e HUD (11/11/2026)

- `Pitaco24Status.cs` agora consulta somente `PitacoConnectionManager.Active.ConnectedVersion == Pitaco24`, evitando que o status de uma instancia `SerialControllerPitaco24` de cena fique desatualizado. O `PitacoStatus.cs` do 2.0 permanece inalterado; os dois indicadores nao devem ficar verdes simultaneamente.
- A SpO2 DEMO foi calculada nos logs Unity enviados pelo teste (99.5%), mas nao cabia no campo visual antigo. O `StatusDevices.prefab`, objeto `Oximetro Status/measures`, tinha `RectTransform` de apenas 83.1 x 35.17. O campo passou para 220 x 57, expandindo para a esquerda e para baixo enquanto preserva o limite superior/direito. A fonte foi ampliada de 13 para 16, e o alinhamento mudou de LowerCenter para UpperRight.
- A formatacao de `Player.OximetryHudText` continua em duas linhas, identificando BPM [2.4] e SpO2 DEMO. Nenhuma alteracao nas formulas, fluxo USB, Player, Mixer, ML-Agents ou banco.
- Verificacao local: entrar em Play Mode; no menu, confirmar que apenas o indicador 2.4 fica verde; na plataforma, confirmar duas linhas visiveis e sem corte. Se houver override de tamanho no objeto da cena, remover o override de `measures` para herdar o prefab atualizado.
- Codigo e prefab conferidos estaticamente no GitHub; testes visuais no Unity com hardware ainda pendentes.
