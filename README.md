<p>Os blocos envelhecerão lentamente com o tempo enquanto estiverem na atmosfera de planetas configurados.</p>
<p>Todos os blocos que não estejam cobertos por outros blocos ou espaços herméticos em todas as direções serão afetados pela envelhecimento.</p>
<p>Por padrão, os planetas Terra, Alien, Pertam e Vênus (que é qualquer planeta modificado que tenha "Vênus" em seu nome) são configurados.</p>
<p>As grids dentro de SafeZones com dano desativado não envelhecerão.</p>
<p>Scripts apenas do lado do servidor! (Deve funcionar em servidores dedicados Xbox, mas não testado)</p>
<br/>
<p><em>!!AVISO!! Este mod ainda é um tanto experimental. Bugs são esperados, por favor reporte se encontrados. Tenha também cuidado ao adicionar isso ao seu mundo já construído sem salvamentos de backup.*</em></p>

## F.A.Q

* Como evitar que um bloco envelhecido se deteriore?
<p>Aplique uma textura que não esteja na lista de estágios de deterioração do planeta onde você se encontra, antes que sua grid começe a tomar dano.</p>

* O clima influencia na deterioração?
<p>Não. Os efeitos climáticos não foram implementados, pelo mod usado como referência e eu também não implementei ainda para tentar manter uma boa performance no jogo.</p>

## Configuração

<p>Este mod pode ser configurado por jogo salvo. Crie e salve o jogo com este mod adicionado. Abra o diretório de armazenamento do seu save em :</p>
`C:\Users\<UserName>\AppData\Roaming\SpaceEngineers\Saves\<RandomNumber>\<SaveGameName>\Storage\<RandomNumber>.sbm_ExtremeConditions`
<p>O arquivo <strong>config.xml</strong> está dentro da pasta, contendo uma configuração padrão.</p>
<p><strong>Certifique-se de editar a versão mais recente se houver mais de uma!</strong></p>
<p>Também é necessário reconfigurar sempre que o mod for atualizado com alterações nas opções de configuração.</p>
<p>Utilize o editor de texto da sua preferência.</p>
<br/>
<p>Você verá a seguinte configuração padrão:</p>

```xml
  <OnlyAgedUnpoweredGrids>false</OnlyAgedUnpoweredGrids>
  <AgingDamagesBlocks>false</AgingDamagesBlocks>
  <NoMercy>false</NoMercy>
  <Planets>
    <Planet>
      <PlanetNameContains>Earth</PlanetNameContains>
      <AgingRate>300</AgingRate>
      <OnlyAgedUnpoweredGrids>true</OnlyAgedUnpoweredGrids>
    </Planet>
    <Planet>
      <PlanetNameContains>Alien</PlanetNameContains>
      <AgingRate>180</AgingRate>
      <AgingStages>
        <string>Mossy_Armor</string>
        <string>Rusty_Armor</string>
        <string>Heavy_Rust_Armor</string>
      </AgingStages>
    </Planet>
    <Planet>
      <PlanetNameContains>Triton</PlanetNameContains>
      <AgingRate>100</AgingRate>
      <AgingStages>
        <string>Mossy_Armor</string>
        <string>Frozen_Armor</string>
      </AgingStages>
    </Planet>
    <Planet>
      <PlanetNameContains>Pertam</PlanetNameContains>
      <AgingRate>60</AgingRate>
      <AgingStages>
        <string>Dust_Armor</string>
        <string>Rusty_Armor</string>
        <string>Heavy_Rust_Armor</string>
      </AgingStages>
    </Planet>
    <Planet>
      <PlanetNameContains>Venus</PlanetNameContains>
      <AgingRate>50</AgingRate>
    </Planet>
  </Planets>
  <BlockSubtypeContainsBlackList>
    <string>Concrete</string>
    <string>Wood</string>
  </BlockSubtypeContainsBlackList>
```

### Parâmetros

- OnlyAgedUnpoweredGrids - Se definido como verdadeiro, envelhecerá apenas grades sem energia (abandonadas).
- AgingDamagesBlocks - Os blocos começaram a sofrer dano após atingir o último estágio de envelhecimento. Se estiver desligado (false), apenas a texture será afetada.
- NoMercy - Independente de AgingDamagesBlocks estar habilitado, se NoMercy estiver desabilitado, o mod protegerá os RespawnShips do dano por envelhecimento, afetando apenas a textura.
- Planets - Planetas vanilla ou modded podem ser adicionados a lista, PlanetNameContains servirá para encontrar qualquer planeta que contenha o nome citado. 
- AgingRate - Taxa de envelhecimento, não é precisa, mas quanto menor, mais rápido (e mais lag pode gerar no seu server).
- AgingStages é uma lista de estágios de envelhecimento, caso não definida na configuração utilizará "Rusty_Armor" e "Heavy_Rust_Armor" (nessa ordem). 
- BlockSubtypeContainsBlackList - Blocos cujo subtipo contém qualquer string desta lista, não sofrerá de envelhecimento.

<!-- 
## Integrations

Any modded planet that has atmosphere can be used with this mod.

Any modded block will rust if it supports textures.

To make rust maintenance more realistic it is recomended to use this mod together with [url=https://steamcommunity.com/sharedfiles/filedetails/?id=500818376]Paint Gun[/url] mod, while [url=https://steamcommunity.com/sharedfiles/filedetails/?id=2046319599]disabling vanilla painting[/url]
 -->

## Reconhecimentos

- Esse Mod se originou do Mod [Rust Mechanics](https://steamcommunity.com/sharedfiles/filedetails/?id=2761947340&searchtext=rust+mechanics) do [Bačiulis](https://steamcommunity.com/id/laggorazh) 
- O Mod do qual esse se originou é baseado no script Atmospheric Damage do **Rexxar**. Não foi possível encontrar o link original, se alguém tiver, por favor me avise.

<!-- 
Ships in screenshots:
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=2562576691]Astron, interplanetary tanker/hauler (No mods)[/url] by OctoBooze
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=2617139013]“Frontier” Scientific Research Exploration System(No Mod)[/url] by ARC17Alpha
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=2652038922]SpaceX Starship (1:1 scale)[/url] by me
 -->

## Relatando Problemas

<p>Por favor, relate quaisquer problemas que encontrar para ajudar a melhorar este mod.</p>
<p>Ao relatar, tenha o cuidado de fornecer todas as informações possíveis, para que eu possa depurar e corrigir.</p>
<br/>
<p>Estas são as informações mínimas necessárias ao relatar um problema:</p>
<br/>
- Lista de todos os mods usados em um jogo na ordem exata (faça uma captura de tela do menu de mods do jogo salvo)
- Registro mais recente, logo após o problema ou falha. O registro está localizado:
`"C:\Users\<UserName>\AppData\Roaming\SpaceEngineers\SpaceEngineers_<datetime>.log"`
- Captura de tela do problema se estiver visível no jogo
<br/>
<p>Forneça quaisquer informações/detalhes adicionais que possam ajudar.</p>
<br/>
<p>Use serviços online como Imgur, pastebin ou outros para compartilhar: capturas de tela, logs, etc.</p>