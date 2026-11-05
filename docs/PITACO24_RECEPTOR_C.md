# PITACO 2.4 — Etapa 1: receptor C (Unity)

## Contrato de identificacao

| Dispositivo | Comando enviado pela Unity | Resposta esperada |
|---|---|---|
| PITACO 2.0 (receptor A, inalterado) | `e` | `echop` |
| Oximetro independente (receptor B, inalterado) | `e` | `echox` |
| PITACO 2.4 (novo receptor C) | `e` | `echo2.4p` |

**Por que essa resposta?** Os controladores originais procuram `echop`/`echox` por substring. `echo2.4p` nao contem nenhuma delas e e comparada com igualdade exata pelo receptor C.

## Fluxo desta etapa

```
ESP32-C3 (MPX5010DP + MAX30102)
  -- JSON de amostras, por USB ou BLE --> Bridge Python
  -- uma COM virtual, 115200 baud --> SerialControllerPitaco24.cs
  -- evento OnJsonReceived(string) --> [proxima etapa: parser/integrador]
```

- A Bridge responde `echo2.4p\r\n` ao comando `e` **apenas** quando a opcao `PITACO 2.4 (JSON)` esta selecionada.
- A Bridge **nao** encaminha esse `e` ao firmware, que ainda responde `echop` em sua serial fisica.
- A Bridge envia quadros JSON `sample` e `g1` **integrais**, preservando `ppg.red`, `ppg.ir`, pressao, `seq`, `t_ms`, SQI e demais campos. Nao calcula SpO2 nem descarta o RAW para transmitir so BPM.
- O receptor C se auto-instancia antes das cenas (`BeforeSceneLoad`), descobre a COM e reutiliza a thread serial existente, com fila propria de 512 mensagens e drenagem de ate 64 por frame, para nao usar a fila legada configurada com 1.
- Ao receber a confirmacao de conexao da thread C, envia `r` (iniciar amostragem). Mantem os comandos `f` (parar) e demais comandos na bridge.
- `OnJsonReceived` e o contrato de entrada para o futuro integrador. **Nesta etapa o C ainda nao alimenta os Players, UI, DeepDDA, banco nem calcula BPM/SpO2.**
- A logica de pressao e os receptores A/B nao foram alterados. O novo objeto e global entre cenas e fica em `DontDestroyOnLoad`.

## Validacao manual — Windows, Unity 2022.3.4f1

1. Na Bridge, selecionar `Saida ao jogo: PITACO 2.4 (JSON)`, selecionar USB/BLE e a COM virtual correspondente ao par com0com. Iniciar a Bridge **antes** da Unity.
2. Abrir o Unity Editor com as dependencias ML-Agents locais corrigidas.
3. Iniciar a cena `MainMenu` ou `Plataform` em Play. No Hierarchy, conferir `PITACO 2.4 - Receptor C`. No Console, conferir `[PITACO 2.4] Receptor C identificado em ...`.
4. Com o ESP32 ativo, confirmar que a Bridge recebe o comando `r`. O evento C `OnJsonReceived` deve receber JSON `sample` e `g1`, inclusive `red`/`ir`. Inspecionar por breakpoint no Visual Studio/VS Code, **sem logar amostras continuamente**.
5. Testar ausencia do dispositivo C e um PITACO 2.0/oximetro legado: o C nao deve reivindicar `echop` ou `echox`; A/B continuam funcionando.
6. Desconectar/reconectar a Bridge e verificar retorno da conexao. Verificar a continuidade por `seq`, para detectar qualquer descarte de quadros.

## Limites desta entrega

- Ainda nao existe parser de medidas fisiologicas, roteamento da pressao ao legado, processamento de RAW RED/IR, SQI no Unity, interface grafica, integracao com DeepDDA nem persistencia.
- Reutilizamos `SerialThreadPitaco` para aquisicao: ha uma **fila limitada**; se a Unity ficar travada e ela encher, podera ocorrer descarte. Na proxima fase, validar por `seq` e considerar bufferizacao/estrategia de perda explicita.
- O firmware continua retornando `echop` na USB fisica; `echo2.4p` e uma **identidade da bridge diante do Unity**, independente de USB/BLE.
- Dois receptores ainda podem sondar COMs durante reconexao. A inicializacao antecipada do C reduz a disputa, mas nao prova exclusao total. Testar todas as combinacoes A/B/C nas cenas.
- O PR anterior de modo `P24` resumido (BPM, sem RAW) **nao deve ser aplicado junto sem reconciliar o contrato**; este plano C usa JSON integral.

## Referencias

- [Bridge PITACO](https://github.com/PHV00/PITACO_ESP32)
- [I Blue It PITACO 2.4](https://github.com/PHV00/iblueit-psychophysiological-flow-pitaco2.4-port)
