# I Blue It 5.0 — Marco 1 (protótipo): BPM e SpO2 demonstrativa em C#

> **Pesquisa / protótipo não calibrado.** Os valores produzidos **não são
> medições clínicas** e **não podem** acionar alertas, recompensas ML-Agents,
> regras de dificuldade, decisões de tratamento ou segurança do paciente.
> Nenhuma cena, HUD, firmware, fluxo de pressão ou API foi modificada.

## Objetivo e limites

- Consumir RED/IR do MAX30105 conectado ao PITACO 2.4, recebido pelo
  evento `SerialControllerPitaco24.OnJsonReceived` no mesmo USB/COM.
- Calcular **um novo BPM em C#** a partir do IR, sem usar `ppg.bpm` da
  biblioteca SparkFun já presente no firmware.
- Calcular **SpO2 DEMO** por `R=(RMS(ACred)/DCred)/(RMS(ACir)/DCir)` e pela
  curva de exemplo Maxim/SparkFun. Não é calibração específica deste sensor.
- **Não integrar SQI/G1, HUD, Player, DeepDDA ou armazenamento em nuvem**
  nesta etapa; apenas prevenção de entradas matematicamente inválidas.
- Os logs e nomes das propriedades distinguem `SpO2DemoPercent` de uma
  eventual medida validada. Não mostrar como SpO2 clínica.

## Arquivos

Em `Assets/_Game/Scripts/Core/Oximetry/`:

| Arquivo | Responsabilidade |
|---|---|
| PpgSample.cs | Amostra RED/IR bruta, 18 bits, seq e timestamp |
| PpgSampleBuffer.cs | Janela limitada de 8 s; verifica seq e tempo |
| PpgSignalProcessor.cs | fs efetivo, DC, remoção da média móvel, AC e RMS |
| BpmEstimator.cs | Autocorrelação normalizada no IR, 35–200 BPM |
| Spo2Estimator.cs | Razão AC/DC e curva **DEMO** |
| PpgProcessingCoordinator.cs | Componente Unity opcional, escuta JSON e publica resultado |

Testes NUnit: `Assets/_Game/Scripts/Editor/Unit Tests/PpgProcessingTests.cs`.

## Como ativar para diagnóstico no Unity

1. Abra o projeto no Unity **2022.3.4f1**, na branch de implementação.
2. Na cena `MainMenu`, encontre o objeto com `SerialControllerPitaco24`.
3. **Add Component → PpgProcessingCoordinator** no mesmo objeto; se
   necessário, arraste o `SerialControllerPitaco24` ao campo homônimo.
4. Conecte o PITACO 2.4 normalmente. O componente **apenas observa** os
   JSONs, sem abrir outra COM e sem enviar comandos ao hardware.
5. Após reunir amostras (mínimo 70 e 3 s), veja logs
   `[PPG C# experimental]` no Console a cada ~5 s. `--` = indisponível.
6. Em `Window > General > Test Runner > EditMode`, execute
   `PpgProcessingTests`. Testes sintéticos não comprovam precisão real.

A classe também expõe `LastMeasurement` e `OnMeasurement` para uso
posterior, mas **não há ligação automática com o HUD nem com o MixerManager**.
Para diagnosticar outras cenas, anexe o componente à cena desejada somente
quando formos planejar sua integração e ciclo de vida.

## Algoritmo e parâmetros da primeira versão

- Fonte: JSON `{"type":"sample","seq":123,"t_ms":4567,"ppg":{"red":60000,"ir":80000}}`.
  `g1`, `sqi`, `bpm` e `bpm_valid` não participam da estimativa.
- Janela móvel: 8 s, no máximo 256 amostras; após ~3 s e 70 amostras pode
  ser produzida a primeira estimativa. Recalcular a cada >=400 ms.
- `t_ms` registra a **leitura da FIFO**, não o instante da conversão.
  Várias amostras com `seq` consecutivo podem ter o mesmo timestamp.
  Por isso, `fs` usa (N-1)/duração efetiva da janela, **não mediana**
  dos intervalos entre registros, que seria zero em rajadas.
- Aceitar fs efetivo entre 20 e 30 Hz, coerente com a configuração
  nominal 100 Hz com média FIFO 4 (~25 pares RED/IR/s).
- Reiniciar janela após quebra de `seq`, intervalo de leitura da FIFO
  superior a 750 ms, desconexão ou ausência prolongada de amostras.
- Remover componente lenta por média móvel (~1 s de largura total) e
  suavização de três pontos, conforme o analisador Python experimental.
- BPM: autocorrelação normalizada dos resíduos IR, atraso 35–200 BPM,
  limiar 0,57; preferir menor pico forte (melhor - 0,075),
  interpolação parabólica no atraso.
- SpO2 **DEMO**: `R=(ACredRMS/DCred)/(ACirRMS/DCir)` e
  `-45.060*R^2+30.354*R+94.845`; o método apenas disponibiliza um
  número demonstrativo com R em [0,2; 1,4] e resultado 70–100.
  Isso **não significa** que esteja clinicamente correto.
- BPM e SpO2 DEMO são independentes; saída ausente é `null`,
  não 0 BPM ou 0% de saturação.

### Referências acadêmicas / procedência

1. SparkFun, `spo2_algorithm.h/.cpp`, derivado do exemplo Maxim.
   https://github.com/sparkfun/SparkFun_MAX3010x_Sensor_Library
2. *A Comparison of Reflective Photoplethysmography for Detection of
   Heart Rate, Blood Oxygen Saturation, and Respiration Rate at Various
   Anatomical Locations* (2019).
   https://pmc.ncbi.nlm.nih.gov/articles/PMC6514840/
3. *Effect of Skin Pigmentation and Finger Choice on Accuracy of Oxygen
   Saturation Measurement in an IoT-Based Pulse Oximeter* (2024).
   https://pmc.ncbi.nlm.nih.gov/articles/PMC11174708/
4. Analisador pré-existente no projeto PITACO:
   `PHV00/PITACO_ESP32`:
   `arduino-pitaco-mpx5010dp-ble/pitaco_bridge/experimental.py`.
   O novo BPM segue o método de autocorrelação, **não** o firmware
   `checkForBeat`. A estimativa C# **ainda não foi comparada**
   numericamente com uma captura Python real.

**Importantíssimo:** formulação reconhecida ≠ curva calibrada para o
PITACO 2.4 ≠ precisão experimental aferida ≠ validação clínica.

## Próximos testes físicos (pendentes)

- Reproduzir um CSV real do PITACO 2.4 no C# e comparar janela a janela
  com o Python experimental; registrar diferenças de timestamp/fs.
- Testar dedo estabilizado, sem contato, movimento, remoção e volta
  do dedo, rajadas FIFO e desconexão.
- Comparar BPM com referência externa confiável.
- Comparar SpO2 DEMO com oxímetro validado **apenas para caracterização
  exploratória**; calibração real exige método e referência apropriados.
- Verificar que os logs não afetam a pressão e que não ocorre disputa
  pela porta COM; só então discutir integração visual/ML-Agents.
