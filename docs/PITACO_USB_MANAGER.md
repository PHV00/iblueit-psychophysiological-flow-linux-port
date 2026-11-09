# Descoberta USB unificada — PITACO 2.4 / 2.0

Branch de desenvolvimento: `feature/pitaco-24-integration-unity`.

## Arquitetura

`PitacoConnectionManager` e criado automaticamente pelo Unity antes de carregar
as cenas e preserva uma unica porta USB aberta enquanto o jogo executa.
O manager e o unico responsavel por buscar COM, abrir porta, identificar versao,
ler linhas, escrever comandos, detectar desconexao e tentar reconectar.

- Envia `i` primeiro; aceita somente `echo2.4p` para PITACO 2.4.
- Se nao identificar, na **mesma COM** envia `e`; aceita `echop` como PITACO 2.0.
- Se nenhuma resposta valida chegar, fecha a porta e testa a proxima COM.
- Mantem a porta aberta desde o handshake; **nao reabre entre probe e leitura**.
- Entrega callbacks no thread principal da Unity, nao no thread de I/O.

## Reaproveitamento dos controladores

- `SerialControllerPitaco24.cs`: recebe JSON original, emite `OnJsonReceived`,
  `OnSerialConnected` e demais eventos existentes.
- `SerialControllerPitaco.cs`: mantem eventos de pressao para componentes
  respiratorios, menu e minijogos. Quando conectado um 2.4, recebe **apenas
  linhas numericas de pressao** como camada de compatibilidade. Assim, ambos
  indicadores podem aparecer conectados para um 2.4, mas existe **so uma COM
  fisica aberta**, sem dois handshakes paralelos.
- Quando conectado um 2.0, somente o controlador legado recebe as amostras;
  o controlador 2.4 permanece desconectado.
- Os controladores mantem o modo autonomo como fallback tecnico caso o
  `PitacoConnectionManager` nao esteja presente. O bootstrap automatico
  normalmente o instala em todas as cenas.

## Teste no Windows com Unity 2022.3.4f1

1. `git pull --ff-only origin feature/pitaco-24-integration-unity` no repo Unity.
2. Fechar a interface BLE Python, Monitor Serial e outras aplicacoes que
   ocupem a COM fisica do ESP32-C3.
3. Gravar o firmware **da branch correspondente** do repo `PITACO_ESP32`,
   que responde `i -> echo2.4p` em USB. Firmware antigo nao habilita o
   handshake novo.
4. Abrir a cena `MainMenu` no Unity e iniciar Play.
5. No Hierarchy, conferir o objeto de runtime `Pitaco Connection Manager`
   (DontDestroyOnLoad). Se necessario, informar a COM em `Preferred Port`
   no Inspector antes da proxima busca.
6. Aguardar log `[PITACO Manager] Pitaco24 identificado em COMx`,
   `[PITACO 2.4] Porta serial ativa (manager).` e
   `[PITACO 2.4] JSON: 100`. Conferir o indicador PITACO 2.4.
7. Para testar fallback, usar um PITACO 2.0 fisico com firmware legado
   e conferir `[PITACO Manager] Pitaco20 identificado`.
8. Desconectar e reconectar o cabo para verificar recuperacao, contador
   de JSON e ausencia de erro de COM em uso.

O codigo nao altera firmware, BLE, sensores, SQL, SQI, SpO2 nem os modelos
de ML-Agents. Os dados de SpO2 continuam experimentais e nao clinicos.

## Escopo de verificacao

O roteamento serial e preparado para preservar eventos do jogo, mas a
compilacao Unity e as provas de hardware precisam ser executadas no Windows.
A ordenacao dos handshakes e a exclusividade da COM devem ser confirmadas no
Console com ESP32-C3 e, idealmente, um PITACO 2.0 fisico.
