# Experimento — estimadores PPG sem bloqueio por SQI/contato (apos Marco 0.5)

**Branch principal de experimentacao:** `feature/ppg-csharp-bpm-spo2-mvp`  
**Referencia original imutavel:** `marcos/marco-0.5` (commit `7085a0dc3c7d263c4c0999b9a9d9441dbac6f44f`)  
**Branch de preservacao:** `experiments/ppg-com-controle-qualidade` (criada a partir do Marco 0.5)

## Objetivo

Desacoplar, durante testes de bancada, a decisao de qualidade de sinal da
execucao matematica dos estimadores. Esta alteracao e **somente no Unity**.
O firmware PITACO 2.4 continua transmitindo `sqi.contact`, `sqi.g1` e
`sqi.reason`; seu Gate 01 e os comandos USB/BLE nao foram alterados.

## Alteracoes

- `PpgProcessingCoordinator.ReceiveJson`: `sqi.contact` continua registrado
  em `OpticalContactConfirmed` e no contador `semContato`, mas **nao limpa
  o buffer nem impede estimativas**, mesmo quando e falso.
- `sqi.g1` ja nao condicionava o processamento Unity e continua sem condicionar.
- `PpgSample.TryCreate`, `PpgSampleBuffer`, `PpgSignalProcessor`,
  `BpmEstimator` e `Spo2Estimator` continuam com limites de representacao
  ADC, continuidade sequencial/temporal, janela 8 s, taxa 20–30 Hz,
  denominadores matematicos, periodicidade e limites das curvas.
- A HUD das cenas Plataforma, WaterGame e CakeGame pode exibir candidatos mesmo
  com contato falso. Sem contato, usa rotulos `BPM CAND. S/CONTATO` e
  `SpO2 DEMO CAND.`. Os numeros nao sao medicoes.
- O diagnostico do Unity exibe `gateContato=DESATIVADO_TESTE`.

## Riscos, restricoes e interpretacao

**Nao utilizar em pacientes nem em protocolos de seguranca.**
Sem a exigencia de contato, ruido ambiente ou ausencia do dedo podem gerar
valores aparentemente plausiveis. Esses valores devem ser tratados como
**candidatos matematicos nao confiaveis**. SpO2 continua sendo DEMO,
nao calibrada. Nao devem ser conectados ao `MixerManager`, `DeepDDAAgent`,
recompensas, alertas, niveis de dificuldade, logs clinicos ou paineis
de interrupcao por risco.

A ausencia de `--` nao constitui evidencia de qualidade.
`--` continua ocorrendo por janela insuficiente, falha de sequencia/timestamp,
valores ADC invalidos, taxa fora de 20–30 Hz ou falha matematica dos estimadores.

## Validacao recomendada (Unity Test Runner + hardware)

1. Rodar `PpgProcessingTests`, incluindo o novo teste de estimadores com
   `contact=false` e o teste de identificacao de candidatos na HUD.
2. Abrir o menu e testar com PITACO 2.4 real sem dedo: a janela pode ser
   calculada, mas os resultados devem permanecer identificados como candidatos.
   Se o sinal nao tiver periodicidade, `--` e correto.
3. Inserir o dedo, manter contato por mais de 8 s e comparar estimativas e
   estados de contato com as leituras registradas no firmware.
4. Retirar o dedo e confirmar que a HUD rotula `S/CONTATO`, nao apresenta
   resultados como medidas seguras e nao dispara alerta ou recompensa.
5. Testar desconexao USB, quebra de `seq` e atraso de amostras.
6. Comparar gravacoes RED/IR nos dois branches para medir falsos candidatos
   e escolher futuramente uma politica de publicacao fundamentada em SQI.

## Como recuperar a implementacao anterior

```bash
git switch experiments/ppg-com-controle-qualidade
# ou usar o checkpoint
git switch marcos/marco-0.5
```

Para continuar o desenvolvimento, usar `feature/ppg-csharp-bpm-spo2-mvp`.
Nenhuma alteracao foi feita nos arquivos de firmware PITACO, no protocolo USB,
nos estimadores fisiologicos ou nos parametros de atualizacao visual.
