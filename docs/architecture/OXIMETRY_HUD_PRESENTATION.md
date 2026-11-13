# UI de oximetria — refatoracao do Marco 01

**Base:** `marcos/marco-1` / `0f904d5eff0ad36d401a19aa99943edb867218d9`  
**Escopo:** unificacao da **apresentacao** da HUD. Nao altera firmware,
Gate 01, estimadores, janela PPG, temporizador do Player ou DeepDDA.

## Por que ha tres arquivos OximeterMeasures?

Unity armazena a referencia de cada componente nos arquivos de cena por GUID
do script MonoBehaviour. Os componentes `OximeterMeasuresPlataform.cs`,
`OximeterMeasuresWaterGame.cs` e `OximeterMeasuresCakeGame.cs` **ja existem**
e apontam para classes `Player` distintas. Elimina-los exigiria
migracao/edicao dos componentes serializados nas tres cenas.

Agora cada arquivo e um adaptador fino: encontra o `Player` da cena,
oferece o texto legado e herda toda a logica de UI da classe compartilhada.
Os GUIDs, objetos e componentes de cena existentes sao preservados.

## Arquitetura atualizada

```text
PITACO 2.4 -> PitacoConnectionManager
             |-- PpgProcessingCoordinator: contato, G1, janela,
             |   validacao e estimativas (sem mudanca)
             |-- FingerPlacementMonitor: estados de orientacao
             |   (read-only na HUD; nao autoriza calculos)
             |
             v
    Players existentes: cadencia visual numerica 1 s
             |
             v
Shared/UI/OximetryHudPresenter.cs: compoe/status da UI
     ^             ^                 ^
     |             |                 |
  Oximeter      Oximeter          Oximeter
  Measures      Measures          Measures
  Plataform     WaterGame         CakeGame
     |             |                 |
     +-------------+-----------------+
                   |
             StatusDevices
             Oximetro Status/measures (Text)
```

- `OximetryHudPresenter` e a unica implementacao da UI numerica
  do PITACO 2.4; trata estados de conexao, JSON, amostras, contato,
  feedback de posicionamento e formato `HR [2.4]` / `SpO2 DEMO`.
- `FingerPlacementMonitor.State` informa mensagens durante **ausencia
  de contato**: `Warning/Critical/PauseRequired` -> reposicione,
  `Recovering` -> estabilizando, `TelemetryLost` -> sem telemetria.
- `PpgProcessingCoordinator` continua autoridade exclusiva para
  permitir BPM/SpO2 DEMO. O presenter **nao altera contato/G1** nem
  insere limiares de sinal.
- O texto de medidas validas continua sendo preparado pelo `Player`,
  cuja cadencia experimental continua em 1 s e invalidacao e imediata.
- A API estatica `Player.FormatPitaco24Oximetry` permanece disponivel
  para os minijogos e testes, delegando a formatacao ao presenter.
- O oximetro legado independente continua usando `oxiActive`,
  `OximetryHudText` e `O;10`.
- O presenter evita chamadas `GetComponent<Text>()` a cada frame
  e nao reatribui `Text.text` se o conteudo nao mudou.
- `FingerPlacementFeedback` continua dono da sobreposicao orientativa
  e `FingerPlacementPauseBridge` continua dono do pedido de pausa.
  A HUD numerica nao instancia outro overlay nem assume a pausa.

## O que esta fora do escopo

As tres classes `Player` **ainda contem** logica semelhante de
temporizacao 1 s e leitura do `PpgProcessingCoordinator`.
Refatora-las numa segunda etapa exigira estudar os consumidores de
`oxiActive`, `HRValue`, `SPO2Value` e o comportamento legado, para
nao interromper o fluxo respiratorio e o Mixer. Nao misturar essa
migracao com o ajuste de UI.

Nao foi corrigido o descarte de janelas, os criterios de contato
ou a expiracao do G1; nao atribuir a este commit melhorias de SQI.

## Checklist de regressao Unity

1. Executar `PpgProcessingTests`, incluindo os testes do presenter.
2. Conferir no Editor os componentes de `StatusDevices/measures` nas
   cenas `Plataform`, `WaterGame`, `CakeGame`. Os scripts existentes
   devem continuar vinculados sem `Missing Script`.
3. PITACO 2.4 conectado: verificar espera por JSON, contato ausente,
   `--` sem estimativa, BPM/SpO2 DEMO em duas linhas e periodo 1 s.
4. Retirar dedo: verificar texto de reposicionamento quando o monitor
   passar a Warning/Critical; conferir overlay independente.
5. Testar pausa opcional e estado `TelemetryLost`, sem liberar a pausa
   legado indevidamente.
6. Sem PITACO 2.4, testar o oximetro legado e sua visibilidade original.
7. Confirmar em cada jogo a integridade da pressao, G1 e ausencia de
   novos valores DEMO no Mixer/DeepDDA.

**Validacao local de Unity Editor e hardware pendente:** a inspeccao e
os testes adicionados ao repositorio nao substituem a execucao real.
