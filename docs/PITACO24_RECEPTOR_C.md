# PITACO 2.4 — Etapa 1: receptor C em USB direta no Unity

## Contrato de identificacao

| Dispositivo | Comando enviado pelo Unity | Resposta esperada |
|---|---|---|
| PITACO original A (inalterado) | `e` | `echop` |
| Oximetro independente B (inalterado) | `e` | `echox` |
| PITACO 2.4 C (novo, USB direta) | `i` | `echo2.4p` |

**Importante:** `i` foi escolhido para nao mudar `e/echop` na
Bridge Python e no BLE legado. Nao existe modo C na interface Python.

## Fluxo desta etapa

```
ESP32-C3 (MPX5010DP + MAX30102)
   -- USB Serial fisica 115200 --> SerialControllerPitaco24.cs
   -- JSON por linha (sample/g1) --> OnJsonReceived(string)
   -- [proxima etapa] parser/integrador do jogo
```

- O firmware novo responde `echo2.4p\n` ao receber `i` **somente na USB**.
- O C abre a mesma COM fisica do firmware, sem par COM virtual e sem
  middleware Python, e mantem essa porta aberta **apos a identificacao**
  ate a desconexao, para reduzir corrida com A/B.
- O receptor C se instancia antes das cenas via
  `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`.
- O C envia `r` apos conectar; ao destruir, envia `f`.
- Recebe JSON `sample` com RED/IR e `g1`; ignora as linhas numericas
  legadas de pressao no receptor (o parser/integrador recebera pressao
  do campo JSON `mpx5010dp.pressure_pa`).
- Possui fila limitada configurada com 1024 linhas, drenagem por frame,
  contador de amostras, G1, descartes da fila e descontinuidade de
  `seq`. A cada 250 amostras, emite um resumo no Console.
- **Nao** envia os valores ao jogo, ao oxímetro legado, ao ML-Agents,
  ao banco de dados nem calcula SpO2. Essa e a proxima etapa.

## Procedimento de teste (Windows)

1. Atualizar a branch de firmware
   `PHV00/PITACO_ESP32:feature/pitaco24-usb-identity-direct`.
2. Compilar e gravar o sketch canonico `frimeware2.4` no ESP32-C3.
3. Com o Unity e a Bridge fechados, abrir o Monitor Serial a 115200.
   Enviar `i`, verificar `echo2.4p`; enviar `e`, verificar `echop`.
   Fechar o Monitor Serial.
4. Fechar a Bridge Python (a USB do ESP32 deve ficar livre). Abrir o fork
   Unity `PHV00/iblueit-psychophysiological-flow-pitaco2.4-port` na
   branch `feature/pitaco24-receptor-c-echo`.
5. Iniciar Play numa cena comum. Observar no Console:
   `[PITACO 2.4] echo2.4p reconhecido diretamente em COM...`.
6. Aguardar aproximadamente 250 amostras de PPG (taxa nominal de ~25Hz)
   e verificar a linha `[PITACO 2.4] JSON USB: 250 amostras...`.
7. Desconectar/reconectar, conferir retorno da identificacao e observar
   `lacunas` e `fila descartada`.
8. **Teste de regressao separado:** fechar Unity e executar a Bridge
   Python antiga por BLE em `Somente monitor`, testando os dois
   graficos. Os arquivos BLE/Bridge nao foram modificados pelo PR de
   firmware novo.

## Limites e riscos

- O C usa **USB fisica** nesta etapa, nao uma COM virtual e nao BLE.
- A/B permanecem intocados. A inicializacao antecipada de C diminui
  disputas de porta, mas o hot-plug concorrente de A e C requer ensaio.
- Com varias portas COM, a sondagem leva ate o timeout configurado por
  porta; usa `i`, que dispositivos legados ignoram.
- O limite de fila implica possivel descarte se o Unity atrasar. Os
  contadores mostram perdas; o futuro SQI deve considera-las.
- O firmware continua enviando pressao numerica + JSON simultaneamente.
- Nao houve compilacao do editor Unity nem validacao fisica automatica.

## Referencias

- [Firmware PITACO 2.4](https://github.com/PHV00/PITACO_ESP32)
- [Fork I Blue It PITACO 2.4](https://github.com/PHV00/iblueit-psychophysiological-flow-pitaco2.4-port)
