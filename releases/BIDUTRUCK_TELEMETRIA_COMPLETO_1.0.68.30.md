# BiduTruck Telemetria 1.0.68.30 — instalação completa

Este pacote instala o aplicativo, o dashboard e o TruckSim GPS sem exigir uma versão anterior. Também serve para reinstalar bases antigas, incluindo 1.0.68.12. Ele usa o mesmo executável e o mesmo dashboard conferidos na instalação do responsável.

Feche o BiduTruck e o TruckSim GPS antes de instalar. O instalador reutiliza a pasta registrada, quando disponível; confirme que ela corresponde à instalação que você usa. Ao substituir uma instalação, cria uma pasta `backup-completa-1.0.68.30-*` com cópia e conferência dos arquivos anteriores. Não exclui nem reinicia os dados de perfil, credenciais, configurações, saves, fila ou diário em AppData. O launcher inicia o aplicativo e o TruckSim GPS. Em um PC novo, confira a instalação do plugin de telemetria no ETS2 pelo TruckSim GPS.

Requer Windows x64 e .NET Framework 4.8. O runtime Microsoft WebView2 pode ser necessário para os painéis de navegador; é um componente do Windows/Microsoft separado do aplicativo. O pacote inclui as bibliotecas WebView2 do aplicativo, mas não um instalador offline do runtime do navegador. Não inclui nem modifica o ETS2.

Essa instalação completa não é uma correção de reancoragem: a sequência Gold já bloqueada na Central do Xuxa continuará exigindo diagnóstico e recuperação autenticada. Não apague arquivos de diário ou credenciais para tentar contornar esse bloqueio.

Validação: 471 arquivos do payload conferidos por SHA-256 após uma instalação vazia e uma reinstalação sobre um arquivo sintético com metadados de versão 1.0.68.12. O arquivo sintético comprova ausência da barreira de versão; não substitui um teste de ponta a ponta no PC do Xuxa. Backup anterior e arquivo local de controle foram preservados. Os testes de cópia usaram uma compilação de validação sem registro de desinstalação/atalhos, isolada no workspace, porque o sandbox bloqueia alterações no Registro do Windows. O pacote distribuído mantém o registro e a desinstalação convencionais.

Nenhum token, perfil de motorista, chave local ou caixa-preta foi incluído no pacote. O atualizador antigo permanece disponível apenas para bases 1.0.68.24 a 1.0.68.30; o botão principal do site passa a oferecer o completo.
