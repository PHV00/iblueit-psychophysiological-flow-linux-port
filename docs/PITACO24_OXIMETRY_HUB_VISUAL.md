# PITACO 2.4 — integração de oximetria: etapa 1, HUD

**Status:** implementado em branch experimental; compilação no Unity e ensaio físico pendentes.
**Sem uso clínico.** SpO2 DEMO não está calibrada nem validada para paciente.

## Arquitetura ativada automaticamente

O `PitacoConnectionManager.Bootstrap()` cria no mesmo GameObject persistente:

1. `PitacoConnectionManager` — única conexão física USB/COM.
2. `SerialControllerPitaco24` — recebe JSON óptico pelo manager existente.
3. `PpgProcessingCoordinator` — janela de 8 s, cálculo de BPM e SpO2 DEMO.
4. `OximetryIntegrationHub` — publica leituras tipadas, mantendo fonte, versão e status.

Não é preciso anexar `PpgProcessingCoordinator` manualmente nem editar cenas.
Na troca de cena a conexão e a janela de processamento permanecem no manager.
Não crie uma segunda instância do controlador/coordenador nas cenas.

## Consumo visual atual

`OximeterMeasuresPlataform.cs`, `OximeterMeasuresWaterGame.cs` e
`OximeterMeasuresCakeGame.cs` consultam primeiro o hub:
- Se PITACO 2.4 **conectado**, mostram BPM e SpO2 **DEMO** do hub.
- Durante contato ausente, janela incompleta ou interrupção, mostram `--`.
- Se 2.4 **desconectado**, mantêm o comportamento legado do oxímetro separado.
- As mensagens de estado/substituição são imediatas, sem esperar 6 s.

`OximetryHudFormatter` reutiliza os objetos `Text` e os scripts já presentes
nessas três cenas. Não modifica o layout nem as variáveis estáticas do `Player`.
`LeavesGame` não possuía um script de mostrador de oximetria correspondente;
não foi criado um HUD nessa cena nesta etapa.

## Cadências e validade

- Aquisição de RED/IR: ~25 amostras/s.
- `PpgProcessingCoordinator`: 8 s de janela completa, tenta recalcular a cada 400 ms.
- `OximetryIntegrationHub.OnEstimate`: estimativas calculadas, sem o filtro de 6 s,
  reservado para futuro registro de pesquisa, mas **nenhum logger foi conectado**.
- `OximetryIntegrationHub.OnDisplayReading`: novas leituras numéricas
  seguindo `SignalTreatmentDb.LoadSignalParameters("O")` — hoje `O;10` = 6 s.
  A primeira leitura válida é publicada sem aguardar mais 6 s.
- Ausência de contato `sqi.contact != true` limpa a janela imediatamente e
  obriga um novo período de 8 s antes de mostrar números após recolocar o dedo.
  O evento G1 continua ignorado; contato não substitui validação de qualidade.
- Indisponibilidade, quebra de sequência, falta de dados ou desconexão eliminam
  imediatamente os números afetados. Nunca inserir `0` por valor ausente.

## O que fica para as próximas etapas

**Etapa 2:** Mixer/DeepDDA — o hub já tem `OnEstimate` e `OnDisplayReading`,
mas NÃO alteramos `Player.sensorValueOxiSPO`, `Player.sensorValueOxiHR`,
`MixerManager`, `MixerAdaptationGrid`, rewards, alertas nem observações ML-Agents.
Para testes usar `shadow mode`, sem efeitos fisiológicos ativos.

**Etapa 3:** armazenamento — `OximetryReading` contém fonte, versão de
algoritmo, timestamp UTC de recepção, timestamp do dispositivo, disponibilidade
independente de BPM/SpO2, R, fs, autocorrelação e status. Nenhuma API, DTO ou
logger foi alterado. Na implementação do banco incluir identificação da sessão,
versão do contrato e dados ausentes; não gravar BPM e SpO2 como um único float.

## Procedimento de validação no Unity

1. Abrir a branch experimental no Unity 2022.3.4f1; aguardar a compilação.
2. Abrir `Window > General > Test Runner > EditMode`; executar `PpgProcessingTests`,
   incluindo 3 testes de formatação do HUD.
3. Abrir `Plataform`, conectar apenas um PITACO 2.4 com firmware compatível
   (handshake `i` / `echo2.4p`; JSON `sample.ppg.red/ir` e `sqi.contact`).
4. Ver no Console `[PITACO Manager] Pitaco24 identificado`, contagem JSON
   e `[PPG C# experimental]` após 8 s de contato estável.
5. Confirmar que o Text do oxímetro existente apresenta `HR [2.4]` e
   `SpO2 DEMO`. O status de conectado não prova qualidade fisiológica.
6. Retirar o dedo, recolocar e desconectar; confirmar `--` imediato e
   janela novamente completa após retorno do contato.
7. Conferir WaterGame e CakeGame, e verificar que o Mixer/ML-Agents
   continuam sem dados experimentais do PITACO 2.4.
8. Comparar resultados com equipamento de referência sincronizado.
   Não usar números experimentais para segurança clínica ou terapia.

Não há Unity Editor ou hardware disponível na execução remota desta mudança.
