# Marco 1.5 — PITACO 2.4 / Unity: tolerancia de contato PPG e consolidacao da branch

**Registro:** 13/11/2026  
**Repositorio:** `PHV00/iblueit-psychophysiological-flow-pitaco2.4-port`  
**Origem:** `feature/ppg-csharp-bpm-spo2-mvp`  
**Destino previsto:** `main` por pull request, com revisao e rastreabilidade  
**Classificacao:** marco de integracao **experimental**, nao validacao clinica ou metrologica.

## Objetivo

Consolidar as entregas da branch desde a base de Marco 0 (integracao do PITACO 2.4) e
introduzir tolerancia a **tres observacoes consecutivas** de `sqi.contact=false` no
processamento PPG do Unity. Evitar perder a janela deslizante de 8 s por uma ou duas
falhas opticas isoladas, sem injetar as amostras rejeitadas como se fossem reais.

## Escopo consolidado da branch de origem

- Novo processamento C# para RED/IR: validacao ADC/seq/timestamp, janela
  deslizante de 8 s, estimativa experimental de BPM por autocorrelacao IR e
  `SpO2 DEMO` nao calibrada por razao AC/DC.
- O `PpgProcessingCoordinator` usa o mesmo evento JSON/USB existente, sem
  adicionar COM independente; `Gate 01` e calculado no firmware, nao no Unity.
- Publicacao permitida apenas com contato optico e resultado G1 recente
  (`g1=true`, `reason=0`, `samples>=50`, idade maxima experimental 6,5 s).
- Integracao de exibicao pelos Players existentes, com cadencia visual de
  aproximadamente 1 s e refatoracao compartilhada em `OximetryHudPresenter`
  para Plataforma, WaterGame e CakeGame.
- `FingerPlacementMonitor` com estados temporizados (aviso 1 s, critico 3 s,
  pausa opcional 6 s, recuperacao 2 s); overlay e propriedade da pausa
  separados de mecanismos legados. A UI nao valida qualidade fisiologica.
- Ajustes auxiliares acumulados na mesma branch: prefab StatusDevices,
  indicadores de PITACO, robustez em respostas da API/etapas, protecoes do
  DeepDDA contra operacoes invalidas e restauracao de componente visual
  SpriteFrameAnimator. Esses arquivos estao incluidos no PR completo,
  mas nao fazem parte da regra de contato do Marco 1.5.

## Mudanca especifica do Marco 1.5: tres falhas consecutivas

**Autoridade:** `Assets/_Game/Scripts/Core/Oximetry/PpgProcessingCoordinator.cs`,
metodo `ReceiveJson()`. O firmware nao foi alterado neste marco.

| Observacoes consecutivas de contato falso | Politica aplicada |
| --- | --- |
| 1 ou 2 | Suspende o valor publicado, preserva a janela anterior e nao armazena os RED/IR suspeitos |
| 3 | Confirma a perda, limpa a janela, invalida as estimativas e revoga o G1 anterior |
| Retorno de `contact=true` antes da 3ª | Zera contador e marca a lacuna curta na primeira amostra boa subsequente |
| Sem objeto SQI / JSON inadequado | Erro de protocolo/validacao; nao interpretar como simples perda optica toleravel |

**Tratamento de lacunas:** `PpgSample.SkippedContactSamplesBefore` registra 1 ou
2 amostras opticas descartadas; `PpgSampleBuffer.Add()` distingue essas
lacunas de saltos inesperados da sequencia. No processamento,
`PpgSignalProcessor.TryProcess()` insere interpolacao linear somente
para as lacunas marcadas e limita a reconstrucoes no maximo 2%
da janela (parametro experimental sem calibracao). `Spo2Estimator`
**rejeita janelas contendo amostras reconstruidas**, evitando gerar
SpO2 demonstrativa a partir de RED/IR artificiais. BPM continua
estritamente experimental. A interpolacao nao recupera informacao
fisiologica efetivamente perdida.

**Cuidado com o G1:** um evento `type=g1` reprovado, invalido ou expirado
segue seus bloqueios proprios, independentemente da tolerancia de contato.
Um `sample.sqi.reason` nao nulo diferente de zero, se G1 estava aprovado,
ainda pode reiniciar sua aprovacao: nao confundir essa regra com falhas
transitorias `contact=false` que retornam antes da terceira leitura.

**Monitor de dedo:** continua recebendo cada valor *bruto* de `contact`.
A mensagem grande de reposicionamento nao depende diretamente da limpeza
do buffer: o `FingerPlacementMonitor` continua temporizando Warning/Critical
e seu overlay. A HUD numerica pode indicar ausencia assim que a perda
e confirmada, antes de o banner de 1 segundo aparecer.

## Parametros de engenharia (nao validados fisiologicamente)

| Parametro | Valor | Local |
| --- | --- | --- |
| Confirmacao de perda | 3 leituras consecutivas | `PpgProcessingCoordinator` |
| Taxa RED/IR esperada | ~25 Hz (~40 ms por amostra) | firmware/diagnostico |
| Janela PPG movel | 8.000 ms + margem de 200 ms | `PpgSampleBuffer` |
| Descontinuidade FIFO permitida | ate 750 ms antes de reset | `PpgSampleBuffer` |
| Restricao de lacunas reconstruidas | <= 2% da janela | `PpgSignalProcessor` |
| Recadencia do calculo | 400 ms, com dados validos | `PpgProcessingCoordinator` |
| Resultado G1 | aproximadamente 5 s; tolerancia 6,5 s | firmware / Unity |
| Aviso / critico / pausa opcional | 1 / 3 / 6 s | `FingerPlacementMonitor` |
| Recuperacao de contato no monitor | 2 s | `FingerPlacementMonitor` |

## Testes adicionados ou atualizados

- `PpgProcessingTests`: testes historicos ajustados para a 3ª falha
  confirmar a perda, e nao a primeira.
- Caso de 2 falhas e retorno a contato: manter G1 e buffer, reconstituir
  lacuna apenas para BPM experimental, bloquear SpO2 DEMO.
- Continuidade da sequencia com lacunas explicitamente marcadas e
  rejeicao de saltos inesperados.
- Rejeicao de uma janela com excesso de lacunas nao consecutivas (>2%).
- `FingerPlacementTests`: suite existente de regressao temporal e pausa.

**Situacao de verificacao:** codigo e testes revisados estaticamente;
nao foram executados o Unity Test Runner, compilacao real no Editor,
testes de hardware, medicao de atraso, calibracao ou comparacao contra
referencia clinica. Resultados de testes nao devem ser presumidos como PASS.

## Roteiro minimo de homologacao na Unity 2022.3.4f1

1. Abrir Unity, checar compilacao e executar `PpgProcessingTests` e
   `FingerPlacementTests` em Test Runner/EditMode; anexar resultados ao PR.
2. Em cada cena suportada, verificar status de conexao, mesma COM,
   HUD de 1 s, recuperacao, desconexao e fallback do oximetro legado.
3. Registrar JSON RED/IR, `seq`, `t_ms`, `sqi.contact`, `sqi.reason`,
   G1 e logs `contatoFalhasSeq`, `lacunasContatoToleradas`,
   `perdasContatoConfirmadas`, `buffer` em capturas de contato estavel,
   1, 2 e 3 falhas consecutivas, e varias falhas esparsas.
4. Confirmar que 1-2 falhas isoladas nao apagam a janela nem produzem
   SpO2 artificial; 3 falhas consecutivas limpam o buffer.
5. Confirmar que a UI mostra Warning/Critical conforme seus tempos
   separados, inclusive quando ja houve reset do buffer.
6. Confirmar que jogo respiratorio, seguranca legada, pausa, DeepDDA,
   menu e pressao operam como antes; a SpO2 DEMO nao alimenta
   logicas clinicas nem dificuldade/recompensa.

## Riscos e futuras decisoes

- A confirmacao de 3 amostras (~120 ms de observacoes a ~25 Hz)
  e um limiar **provisorio**. Deve ser testado contra 2, 4 e 5
  com falsos descartes, latencia, utilizacao e qualidade do BPM.
- Preservar memoria temporal nao significa que cada valor de BPM
  recuperado por interpolacao esteja fisiologicamente validado.
- O teto adicional de 2% de reconstrucoes e provisório.
- O SQI Gate 01 verifica integridade basica e nao valida isoladamente
  pulsos ou SpO2. A SpO2 DEMO nao serve para tratamento ou alertas.
- A branch inclui ajustes adjacentes (UI/DeepDDA/API) que exigem
  regressao ao integrar com main; este marco nao e evidencia de
  compilacao, funcionamento em hardware ou seguranca clinica.

## Rastreabilidade

- Marco 0.5: `docs/marcos/MARCO_0_5_PITACO24_PPG_HUD.md`.
- Marco 01: `docs/marcos/MARCO_01_PITACO24_SQI_HUD_UNITY.md`.
- Regra de contato: commit `c1785a84c14f3e883969ede8809684ca1c3a0939`.
- Integracao de HUD compartilhada: `a06686b44a16d061e63d641549100f339531f82c`.
- Este documento e a atualizacao de testes fecham documentalmente o Marco 1.5;
  o merge para `main` nao transforma a implementacao em algoritmo validado.
