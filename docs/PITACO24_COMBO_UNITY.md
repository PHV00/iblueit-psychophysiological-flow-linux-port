# Integração PITACO 2.4 Combo + I Blue It / DeepDDA (PROTÓTIPO)

## Contrato sem BLE no Unity

A comunicação do ESP32 com o Python pode ocorrer por USB **ou BLE**.
O Unity só conhece **uma COM virtual do PITACO** (`SerialControllerPitaco`),
sem dependência BLE.

```text
ESP32 [MPX5010DP + MAX30102]
    | USB ou BLE JSON
    v
PITACO Bridge Python (modo de saida "PITACO 2.4 Combo")
    | UMA COM do PITACO, 115200 baud, CRLF
    | P24,<pressao>,<bpm_fw_ou_vazio>,<spo2_vazio>,<g1>,<contato>
    v
SerialControllerPitaco.Update()
    |--> OnSerialMessageReceived("<pressao>") -> mecanicas e afericao LEGADAS
    |--> OnPitaco24ComboReceived(frame tipado)
             |--> Player: BPM e qualidade, SpO2 indisponivel
             |--> DeepDDAAgent: duas observacoes SOMENTE com opt-in e retreino
```

O handshake `echop` e comandos `e/r/f/c/z` sao preservados.
O `SerialControllerPitaco` agora aguarda `echop` por uma janela limitada,
ignorando linhas de dados antigas ou `P24` que eventualmente tenham ficado
em fila antes da identificacao.
O protocolo legado numerico (p.ex. `-23.15`) continua funcionando.
O receptor discrimina novos frames por `P24,` e bloqueia quadros
malformados, sem encaminhar strings CSV ao `Parsers.Float` dos minijogos.

## Opcoes preservadas

- PITACO original: COM numerica, comportamento anterior sem alteracoes;
- MANO e CINTA: seus SerialController permanecem;
- Oxímetro legado independente: SerialControllerOximetro e seus eventos
  continuam disponiveis; caso esteja conectado, continua tendo prioridade
  sobre BPM combo nos players;
- PITACO 2.4 Combo: pressao e BPM no mesmo receptor SerialControllerPitaco.
  Entrada USB/BLE e selecionada apenas no middleware Python.

No monitor de HR, a fonte combo exibe `SpO2: -- (não disponível)`, em vez
de 0% ou da curva experimental Python. Sem BPM validado, HR nao aparece.

## DeepDDA / ML-Agents

O DeepDDA continua com seus ramos de acoes, recompensas e observacoes
originais por padrao. Em `DeepDDAAgent` existe a propriedade de Inspector
`includePitaco24BpmObservations` **desligada por padrao**:

- Se ligada, acrescenta **duas observacoes**: BPM do firmware normalizado
  como `(bpm-35)/165` entre 0 e 1, e mascara (1 disponivel / 0 ausente).
- O BPM so e disponivel se veio de G1 + contato valido, com frame fresco
  (<2 s); nenhuma estimativa experimental Python entra neste caminho.
- Ativar exige ajustar a dimensao do vetor em `BehaviorParameters` e
  **treinar novo modelo** do ML-Agents. Um modelo anterior nao pode inferir
  corretamente com um vetor diferente.
- **SpO2 experimental NAO entra nas observacoes nem na recompensa ou nos
  painéis de seguranca do Mixer**; o codigo existente de SpO2 considera
  exclusivamente o oxímetro independente, caso conectado.

Esta branch integra o fluxo de observacao (fonte -> jogo -> agente), mas
**NAO** treina nem valida um novo modelo PPO. Nao utilizar esse prototipo
para decisao clinica automatizada.

## Plano de teste

1. Em Unity 2022.3.4f1, abrir cena original e verificar
   `SerialControllerPitaco` existente, sem adicionar segundo receptor.
2. Configurar o par COM virtual existente (com0com no Windows),
   preservar selecionada a COM do jogo no middleware.
3. Modo antigo "Pressao relativa (Pa)": jogo recebe `-23.15` sem P24.
4. Modo combo: jogo recebe `P24,-23.15,72.5,,1,1`,
   as mecanicas ainda recebem `-23.15` e os monitores exibem BPM.
5. Verificar janela SEM BPM: `P24,-23.15,,,1,1` e perda de
   contato: `P24,-23.15,,,0,0`, sem valores retidos como medicao.
6. Testar minijogos Cake/Water, tela Platform e indicador no menu.
7. Conferir que a SpO2 demo nunca aciona alertas, recompensas ou
   adaptacoes e nao aparece como 0%.
8. No Inspector, manter novo vetor desligado para usar o antigo modelo.
   Para treinar, habilitar vetor e ajustar `BehaviorParameters`/PPO.

## Limites do repositorio

`Packages/manifest.json` ainda aponta ML-Agents para caminhos locais
`C:/Users/Ney/Documents/ml-agents-release_21/...`. Isso requer instalacao
ou migracao consciente das dependencias antes de compilar o projeto Unity
em outro computador. Nenhuma versao de pacote foi alterada automaticamente.

O script C# `Pitaco24ComboFrame` pode ser testado separadamente com:

```sh
dotnet run --project Tests/Pitaco24ComboContract/Pitaco24ComboContract.csproj
```

Isso nao substitui compilar/rodar o projeto Unity.
