# WM-player

## Ícone do projeto

Usei a imagem enviada como referência e adicionei um ícone vetorial em `assets/icon.svg`.

Para Windows, o executável precisa de arquivo `.ico` (SVG sozinho não é usado como ícone do `.exe`).  
Como o repositório não aceita arquivo binário no PR, o `.ico` é gerado automaticamente em tempo de build por `scripts/generate_icon_ico.py` e salvo em `src/WMPlayer/obj/generated/icon.ico`.

## Observação sobre "Abrir com"

O executável antigo estava com nome `WMPlayer.exe`, que conflita com o nome do Windows Media Player (`wmplayer.exe`).  
Agora o binário foi renomeado para `WMPlayerApp.exe` para evitar o Windows abrir o player antigo por engano.

![Ícone do WM-player](assets/icon.svg)
